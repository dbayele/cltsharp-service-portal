# CLT# — Charlotte Service Platform Prototype

This prototype now has four independently deployable application processes:

- `client/` — public resident/business React + TypeScript portal (5173)
- `server/` — resident/public ASP.NET Core .NET 8 API (5050)
- `employee-client/` — separate City employee React console (5174)
- `employee-server/` — separate employee ASP.NET Core .NET 8 API (5051)

The public catalog contains 96 service workflows: the original municipal service-request catalog, expanded Police and Fire Department services, Public Safety reporting/commendation/complaint workflows, and rental registration. Water bill payment is a separate authenticated account function rather than a service-request record.

## Backend separation

The resident API accepts public submissions, My Requests, login-audit events, and authenticated water payments. It contains no employee workflow controller. The employee API independently serves queues, detail, assignment, notes, and status changes. Use **different Auth0 API audiences** and deployment credentials for the two APIs.

Recommended audiences:

```text
Resident API: https://resident-api.cltsharp.charlottenc.co
Employee API: https://employee-api.cltsharp.charlottenc.co
```

## PostgreSQL separation

Four physical PostgreSQL databases are used:

- `cltsharp_service_requests` — general requests, resident profile-to-Stripe-customer mapping, water-payment audit rows, and Auth0 login audit rows.
- `cltsharp_police` — Police Department requests, including crime reports, tips, commendations, complaints, records, alarms, traffic concerns, and other police services.
- `cltsharp_fire` — Fire Department requests, including hazards, hydrants, inspections, system testing/impairments, equipment registration, plan review, reports, permits, and fire-prevention services.
- `cltsharp_airport` — Charlotte Douglas International Airport and Aviation Department passenger, community, accessibility, parking, business, badging, vendor, and airport-service requests.

Police/Fire/Airport/general request routing is server-side. Browser clients never select a database. Crime tips are never associated with the signed-in Auth0 subject.

## Police and Fire service catalog

The prototype includes common municipal Police workflows such as non-emergency crime reporting, crime tips, report supplements, incident/crash report requests, public records, alarm registration and false-alarm appeals, picketing notifications, vacation/property watch, traffic enforcement concerns, neighborhood concerns, off-duty/event staffing, community engagement, and fingerprinting/background-service requests.

Fire Department workflows include anonymous hazard reporting, hydrant requests, water-based fire protection system test/release, performance testing/fees, fire equipment registration, system impairment notification, shop drawing review, foster-home inspection, fire report requests, smoke/CO alarm requests, hazmat/property records, off-duty event staffing, fire truck/education requests, station tours, inspections/reinspections, permits, plan review, special-event review, open-burning inquiries, rapid-entry/key-box requests, and fire-code questions.

## U.S.-only access and IP capture

Both .NET APIs include U.S.-only middleware. In production, configure a **trusted edge proxy/CDN** to calculate country and overwrite/strip client-supplied forwarding headers. The sample configuration expects:

```text
NetworkSecurity__CountryHeader=CF-IPCountry
NetworkSecurity__ClientIpHeader=CF-Connecting-IP
NetworkSecurity__TrustClientIpHeader=true
NetworkSecurity__FailClosedWhenCountryMissing=true
```

Do not enable `TrustClientIpHeader` unless the API can only be reached through that trusted proxy and the proxy strips inbound spoofed versions of the header.

Service submissions store the server-observed source IP and country code in their appropriate database. Water-payment audit rows do the same.

### Auth0 login IP auditing and blocking

`auth0/post-login-action.js` is a tenant-level Auth0 Post-Login Action. It reads Auth0's `event.request.ip` and `event.request.geoip.countryCode`, sends the login result to `POST /api/auth-events/login`, and denies the transaction when the GeoIP country is not `US`.

Configure Auth0 Action secrets:

```text
LOGIN_AUDIT_URL=https://<resident-api>/api/auth-events/login
LOGIN_AUDIT_SECRET=<same long random value as Auth0__LoginAuditSecret>
```

The audit endpoint is exempt from API GeoIP middleware because the HTTP caller is Auth0 infrastructure; it authenticates with `X-Auth-Event-Secret` and persists the end-user IP/country supplied by the Auth0 Action. The Stripe webhook is similarly exempt from GeoIP checks and is authenticated by Stripe's webhook signature.

## Auth0 resident accounts

Auth0 owns signup, login, passwords, MFA, and identity lifecycle. PostgreSQL stores only the Auth0 `sub` necessary to associate account-owned data. Resident functions include Create Account, Sign In, My Requests, and Water Bill Payment.

`client/.env`:

```text
VITE_AUTH0_DOMAIN=charlottenc.co
VITE_AUTH0_CLIENT_ID=v42X1dA5o83fPMhYGHYxIgyoKk4oLaeL
VITE_AUTH0_AUDIENCE=https://resident-api.cltsharp.charlottenc.co
VITE_STRIPE_PUBLISHABLE_KEY=pk_test_...
```

### Employee Auth0 application

`employee-client/.env`:

```text
VITE_AUTH0_DOMAIN=charlottenc.co
VITE_AUTH0_CLIENT_ID=GbpU3R4vgmel4BdsBSFz9wEiMyRfynpN
VITE_AUTH0_AUDIENCE=https://employee-api.cltsharp.charlottenc.co
VITE_EMPLOYEE_API_URL=http://localhost:5051
```

The resident and employee Client IDs are public SPA identifiers. Do not put an Auth0 Client Secret in either Vite application. Server APIs validate tokens using the Auth0 domain and their configured audience.

## Water bill payments with Stripe

The resident portal includes **Pay your water bill**. Authentication is required.

Implemented flow:

1. Resident looks up a water account through `IWaterBillingGateway`.
2. The resident API re-validates the account and allowable amount server-side.
3. The API creates/reuses a Stripe Customer mapped to the resident Auth0 `sub`.
4. The API creates a Stripe PaymentIntent. Raw card/PAN/CVC values never pass through or persist in City code/databases.
5. React uses Stripe Elements. Eligible Apple devices/browsers can display Apple Pay when Apple Pay is enabled in Stripe and the production domain is registered.
6. If the resident opts to save a card, the PaymentIntent uses `setup_future_usage`; Stripe attaches the reusable PaymentMethod to the Customer.
7. Saved cards are listed from Stripe by brand/last-four/expiration and may be selected on a later water payment.
8. Stripe webhook events update the authoritative payment status in `water_payments`.

The current `DevelopmentWaterBillingGateway` is intentionally a stub. **Do not deploy it as an authoritative billing lookup.** Replace it with Charlotte Water's CIS/billing API so the account holder, balance, due date, eligibility, duplicate-payment controls, and payment posting/reconciliation are authoritative.

Server Stripe settings:

```text
Stripe__SecretKey=sk_test_...
Stripe__WebhookSecret=whsec_...
```

Configure the webhook to send at minimum:

```text
payment_intent.succeeded
payment_intent.payment_failed
payment_intent.processing
payment_intent.canceled
```

Webhook URL:

```text
POST https://<resident-api>/api/payments/stripe-webhook
```

For Apple Pay web deployment, enable Apple Pay in Stripe and register/verify the production domain according to the current Stripe Dashboard/documentation.

## One-command local build and startup

From the repository root, the entire development stack can be compiled and started with one script:

```bash
./run-cltsharp.sh
```

The script checks prerequisites, creates missing frontend `.env` files from their examples, installs Node dependencies when needed, builds both React applications, restores/builds both .NET 8 APIs in Release mode, starts and health-checks all four PostgreSQL containers, provisions the separate local employee database roles, starts both APIs, and serves the compiled resident and employee portals on ports 5173 and 5174. Press `Ctrl+C` to stop the apps and PostgreSQL containers while preserving database volumes. To leave PostgreSQL running after exit, run `CLTSHARP_KEEP_DOCKER_RUNNING=1 ./run-cltsharp.sh`.

Required local tools: Docker with Compose v2, Node.js/npm, .NET 8 SDK, Bash, and curl.

## Run databases

```bash
docker compose up -d
```

Development defaults:

```text
General: localhost:5432 / cltsharp_service_requests
Police:  localhost:5433 / cltsharp_police
Fire:    localhost:5434 / cltsharp_fire
Airport: localhost:5435 / cltsharp_airport
```

## Run resident API

```bash
cd server
dotnet restore
dotnet run
```

## Run employee API

```bash
cd employee-server
dotnet restore
dotnet run
```

## Run resident UI

```bash
cd client
cp .env.example .env
npm install
npm run dev
```

## Run employee UI

```bash
cd employee-client
cp .env.example .env
npm install
npm run dev
```

## Important API endpoints

Resident/public API:

```text
POST /api/service-requests                         public/optional Auth0
GET  /api/service-requests/{number}                public
GET  /api/my/requests                              Auth0 resident
POST /api/water-bills/lookup                       Auth0 resident
POST /api/water-bills/payment-intents              Auth0 resident
GET  /api/water-bills/saved-cards                  Auth0 resident
GET  /api/water-bills/payments                     Auth0 resident
POST /api/payments/stripe-webhook                  Stripe signature
POST /api/auth-events/login                        Auth0 Action shared secret
```

Employee API:

```text
GET   /api/requests?scope=all|general|police|fire&status=&serviceCode=&search=
GET   /api/requests/{number}
PATCH /api/requests/{number}
POST  /api/requests/{number}/notes
```

The employee console can process all 80 catalog request types. It provides All Services, General, Police, and Fire queues; service/status filters; tracking/service/assignee search; assignment; status transitions; internal notes; and request-detail audit metadata. The All Services queue only aggregates the databases the current employee is authorized to read.

Recommended Auth0 employee API permissions:

```text
read:requests / write:requests  General municipal services
read:police   / write:police    Police Department
read:fire     / write:fire      Fire Department
read:airport  / write:airport   Airport
```

Legacy `read:public-safety` and `write:public-safety` are accepted as compatibility aliases for both Police and Fire.

## EF Core

Schema changes include source-IP/country columns plus `resident_profiles`, `water_payments`, and `login_audits`. Generate and review migrations before production deployment; do not rely on `EnsureCreated` for a persistent environment.

```bash
cd server
dotnet ef migrations add GeneralInitial --context GeneralServiceRequestDbContext --output-dir Migrations/General
dotnet ef database update --context GeneralServiceRequestDbContext

dotnet ef migrations add PoliceInitial --context PoliceDbContext --output-dir Migrations/Police
dotnet ef database update --context PoliceDbContext

dotnet ef migrations add FireInitial --context FireDbContext --output-dir Migrations/Fire
dotnet ef database update --context FireDbContext
```

The employee API maps the same three request schemas but should use distinct least-privilege PostgreSQL credentials for General, Police, and Fire. In production, restrict each database role to only the operations required by the employee processing workflow.

## Production requirements

Before deployment: replace the development water-billing adapter; configure separate Auth0 applications/APIs and roles; deploy the Auth0 Post-Login Action; configure trusted GeoIP edge headers and block direct origin access; define IP/login audit retention and access policy; use managed secrets; configure Stripe restricted keys where appropriate; enable Stripe webhook verification; register the Apple Pay domain; implement billing reconciliation/refunds; add idempotency/duplicate-payment controls based on the real CIS transaction model; run PCI/privacy/legal review; add immutable security audit logging; perform accessibility and security testing; and generate/review EF migrations.

## Airport services

CLT# includes Charlotte Douglas International Airport workflows for Lost & Found, aircraft-noise complaints, airport feedback, accessibility/accommodation requests, ADA/Title VI complaints, parking, ground transportation, airport tours, community engagement, airport app/account support, public records, badging/credentialing, tenant/vendor access, construction/permit coordination, commercial-vehicle/operator coordination, and concessions/vendor inquiries. Airport requests route to the separate `cltsharp_airport` PostgreSQL database and use `CLT-AV-*` tracking numbers.

Employee Auth0 permissions add `read:airport` and `write:airport`. The employee console includes a dedicated Airport queue in addition to All Services, General, Police, and Fire.

## About CLT# and AWS deployment

The resident portal includes a static **About CLT#** page that documents the complete application and data architecture plus the recommended AWS topology. The recommended production deployment uses Route 53 and ACM, separate CloudFront distributions with private S3 origins for the two React applications, AWS WAF U.S.-only geo rules and rate/threat controls, independent ECS Fargate services behind load balancers for the two .NET APIs, and separate private Multi-AZ RDS for PostgreSQL deployments for General, Police, Fire, and Airport data. Secrets Manager/KMS hold secrets and encryption keys; CloudWatch and CloudTrail provide application/infrastructure observability and audit trails.

Auth0 and Stripe remain external trust services. Auth0 owns identity and authentication; Stripe owns card/wallet payment credentials.
