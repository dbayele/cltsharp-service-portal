#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

APP_PIDS=()
KEEP_DOCKER_RUNNING="${CLTSHARP_KEEP_DOCKER_RUNNING:-0}"

log() {
  printf '\n[CLT#] %s\n' "$*"
}

fail() {
  printf '\n[CLT#] ERROR: %s\n' "$*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command '$1' was not found in PATH."
}

cleanup() {
  local exit_code=$?
  trap - EXIT INT TERM

  if ((${#APP_PIDS[@]})); then
    log "Stopping CLT# application processes..."
    kill "${APP_PIDS[@]}" 2>/dev/null || true
    wait "${APP_PIDS[@]}" 2>/dev/null || true
  fi

  if [[ "$KEEP_DOCKER_RUNNING" != "1" ]]; then
    log "Stopping CLT# PostgreSQL containers (volumes are preserved)..."
    docker compose stop >/dev/null 2>&1 || true
  else
    log "Leaving PostgreSQL containers running because CLTSHARP_KEEP_DOCKER_RUNNING=1."
  fi

  exit "$exit_code"
}

trap cleanup EXIT INT TERM

copy_env_if_missing() {
  local directory="$1"
  if [[ ! -f "$directory/.env" && -f "$directory/.env.example" ]]; then
    cp "$directory/.env.example" "$directory/.env"
    log "Created $directory/.env from .env.example. Review it before production use."
  fi
}

install_node_dependencies() {
  local directory="$1"
  if [[ ! -d "$directory/node_modules" ]]; then
    log "Installing Node dependencies in $directory..."
    if [[ -f "$directory/package-lock.json" ]]; then
      npm --prefix "$directory" ci
    else
      npm --prefix "$directory" install
    fi
  fi
}

wait_for_container_health() {
  local container="$1"
  local max_attempts="${2:-60}"
  local attempt=1
  local status=""

  while (( attempt <= max_attempts )); do
    status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container" 2>/dev/null || true)"
    if [[ "$status" == "healthy" || "$status" == "running" ]]; then
      return 0
    fi
    if [[ "$status" == "unhealthy" || "$status" == "exited" || "$status" == "dead" ]]; then
      fail "Container $container entered state '$status'. Run 'docker logs $container' for details."
    fi
    sleep 2
    ((attempt++))
  done

  fail "Timed out waiting for $container to become healthy."
}

wait_for_http() {
  local url="$1"
  local name="$2"
  local max_attempts="${3:-60}"
  local attempt=1

  while (( attempt <= max_attempts )); do
    if curl --silent --fail --max-time 2 "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
    ((attempt++))
  done

  fail "$name did not become ready at $url."
}

ensure_employee_role() {
  local container="$1"
  local admin_user="$2"
  local database="$3"
  local employee_user="$4"
  local employee_password="$5"

  docker exec -i "$container" psql -v ON_ERROR_STOP=1 -U "$admin_user" -d "$database" >/dev/null <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = '$employee_user') THEN
    CREATE ROLE $employee_user LOGIN PASSWORD '$employee_password';
  ELSE
    ALTER ROLE $employee_user WITH LOGIN PASSWORD '$employee_password';
  END IF;
END
\$\$;
GRANT CONNECT ON DATABASE $database TO $employee_user;
GRANT USAGE ON SCHEMA public TO $employee_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO $employee_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO $employee_user;
ALTER DEFAULT PRIVILEGES FOR ROLE $admin_user IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO $employee_user;
ALTER DEFAULT PRIVILEGES FOR ROLE $admin_user IN SCHEMA public GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO $employee_user;
SQL
}

log "Checking prerequisites..."
require_command docker
require_command node
require_command npm
require_command dotnet
require_command curl
docker compose version >/dev/null 2>&1 || fail "Docker Compose v2 is required ('docker compose')."

copy_env_if_missing client
copy_env_if_missing employee-client

install_node_dependencies client
install_node_dependencies employee-client

log "Building resident React application..."
npm --prefix client run build

log "Building employee React application..."
npm --prefix employee-client run build

log "Restoring and building resident .NET API..."
dotnet restore server/Charlotte.ServiceRequests.Api.csproj
dotnet build server/Charlotte.ServiceRequests.Api.csproj --configuration Release --no-restore

log "Restoring and building employee .NET API..."
dotnet restore employee-server/Charlotte.EmployeeRequests.Api.csproj
dotnet build employee-server/Charlotte.EmployeeRequests.Api.csproj --configuration Release --no-restore

log "Starting PostgreSQL containers..."
docker compose up -d

for container in \
  cltsharp-service-requests-postgres \
  cltsharp-police-postgres \
  cltsharp-fire-postgres \
  cltsharp-airport-postgres; do
  wait_for_container_health "$container"
done

log "Ensuring least-privilege employee development database roles exist..."
ensure_employee_role cltsharp-service-requests-postgres cltsharp_app cltsharp_service_requests cltsharp_employee change-me-employee
ensure_employee_role cltsharp-police-postgres cltsharp_police_app cltsharp_police cltsharp_police_employee change-me-employee-police
ensure_employee_role cltsharp-fire-postgres cltsharp_fire_app cltsharp_fire cltsharp_fire_employee change-me-employee-fire
ensure_employee_role cltsharp-airport-postgres cltsharp_airport_app cltsharp_airport cltsharp_airport_employee change-me-employee-airport

log "Starting resident API on http://localhost:5050..."
dotnet run --project server/Charlotte.ServiceRequests.Api.csproj --configuration Release --no-build --launch-profile http &
APP_PIDS+=("$!")
wait_for_http http://localhost:5050/health "Resident API"

# Ensure grants also cover any tables created by the resident API during startup.
ensure_employee_role cltsharp-service-requests-postgres cltsharp_app cltsharp_service_requests cltsharp_employee change-me-employee
ensure_employee_role cltsharp-police-postgres cltsharp_police_app cltsharp_police cltsharp_police_employee change-me-employee-police
ensure_employee_role cltsharp-fire-postgres cltsharp_fire_app cltsharp_fire cltsharp_fire_employee change-me-employee-fire
ensure_employee_role cltsharp-airport-postgres cltsharp_airport_app cltsharp_airport cltsharp_airport_employee change-me-employee-airport

log "Starting employee API on http://localhost:5051..."
dotnet run --project employee-server/Charlotte.EmployeeRequests.Api.csproj --configuration Release --no-build --launch-profile http &
APP_PIDS+=("$!")
wait_for_http http://localhost:5051/health "Employee API"

log "Starting compiled resident portal on http://localhost:5173..."
npm --prefix client run preview -- --host 0.0.0.0 --port 5173 &
APP_PIDS+=("$!")

log "Starting compiled employee portal on http://localhost:5174..."
npm --prefix employee-client run preview -- --host 0.0.0.0 &
APP_PIDS+=("$!")

wait_for_http http://localhost:5173 "Resident portal"
wait_for_http http://localhost:5174 "Employee portal"

cat <<'STATUS'

CLT# is running:
  Resident portal:  http://localhost:5173
  Employee portal:  http://localhost:5174
  Resident API:     http://localhost:5050
  Employee API:     http://localhost:5051
  General DB:       localhost:5432
  Police DB:        localhost:5433
  Fire DB:          localhost:5434
  Airport DB:       localhost:5435

Press Ctrl+C to stop the application processes and PostgreSQL containers.
Set CLTSHARP_KEEP_DOCKER_RUNNING=1 before running the script if you want the
PostgreSQL containers to remain running after the application processes stop.
STATUS

wait
