# CLT# — Charlotte Service Platform Prototype

This prototype has four independently deployable application processes:

- `client/` — public resident/business React + TypeScript portal (5173)
- `server/` — resident/public ASP.NET Core .NET 8 API (5050)
- `employee-client/` — separate City employee React console (5174)
- `employee-server/` — separate employee ASP.NET Core .NET 8 API (5051)

The public catalog contains 96 service workflows spanning municipal services, Police, Fire, Charlotte Douglas International Airport, Public Safety reporting/commendation/complaint workflows, and rental registration. Water bill payment is a separate authenticated account function rather than a service-request record.

## Backend separation

The resident API accepts public submissions, My Requests, login-audit events, and authenticated water payments. It contains no employee workflow controller. The employee API independently serves queues, detail, assignment, notes, and status changes. Use different Auth0 API audiences and deployment credentials for the two APIs.

Recommended audiences:

```text
Resident API: https://resident-api.cltsharp.charlottenc.gov
Employee API: https://employee-api.cltsharp.charlottenc.gov
```

## PostgreSQL separation

Four physical PostgreSQL databases are used:

- `cltsharp_service_requests` — general requests, resident profile-to-Stripe-customer mapping, water-payment audit rows, and Auth0 login audit rows.
- `cltsharp_police` — Police Department requests, including crime reports, tips, commendations, complaints, records, alarms, traffic concerns, and other police services.
- `cltsharp_fire` — Fire Department requests, including hazards, hydrants, inspections, system testing/impairments, equipment registration, plan review, reports, permits, and fire-prevention services.
- `cltsharp_airport` — Charlotte Douglas International Airport and Aviation Department passenger, community, accessibility, parking, business, badging, vendor, and airport-service requests.

Police/Fire/Airport/general request routing is server-side. Browser clients never select a database. Crime tips are never associated with the signed-in Auth0 subject.

## U.S.-only access and IP capture

Both .NET APIs include U.S.-only middleware. In production, configure a trusted edge proxy/CDN to calculate country and overwrite/strip client-supplied forwarding headers. The sample configuration expects `CF-IPCountry` and `CF-Connecting-IP` style headers. Do not enable trusted client-IP headers unless the API can only be reached through that trusted proxy and the proxy strips inbound spoofed versions.

Service submissions store server-observed source IP and country code in their appropriate database. Water-payment audit rows do the same.

### Auth0 login IP auditing and blocking

`auth0/post-login-action.js` is a tenant-level Auth0 Post-Login Action. It reads Auth0's `event.request.ip` and `event.request.geoip.countryCode`, sends the login result to `POST /api/auth-events/login`, and denies the transaction when the GeoIP country is not `US`.

Configure Auth0 Action secrets:

```text
LOGIN_AUDIT_URL=https://<resident-api>/api/auth-events/login
LOGIN_AUDIT_SECRET=<same long random value as Auth0__LoginAuditSecret>
```

## Auth0 resident accounts

Auth0 owns signup, login, passwords, MFA, and identity lifecycle. PostgreSQL stores only the Auth0 `sub` necessary to associate account-owned data. Resident functions include Create Account, Sign In, My Requests, and Water Bill Payment.

Resident SPA:

```text
VITE_AUTH0_DOMAIN=charlottenc.co
VITE_AUTH0_CLIENT_ID=v42X1dA5o83fPMhYGHYxIgyoKk4oLaeL
VITE_AUTH0_AUDIENCE=https://resident-api.cltsharp.charlottenc.gov
```

Employee SPA:

```text
VITE_AUTH0_DOMAIN=charlottenc.co
VITE_AUTH0_CLIENT_ID=GbpU3R4vgmel4BdsBSFz9wEiMyRfynpN
VITE_AUTH0_AUDIENCE=https://employee-api.cltsharp.charlottenc.gov
VITE_EMPLOYEE_API_URL=http://localhost:5051
```

The resident and employee Client IDs are public SPA identifiers. Do not put an Auth0 Client Secret in either Vite application. Server APIs validate tokens using the Auth0 domain and configured audience.

## Water bill payments with Stripe

The resident portal includes Pay your water bill. Authentication is required. The API creates/reuses a Stripe Customer mapped to the resident Auth0 subject and creates server-side PaymentIntents. Raw card numbers and CVC values never pass through or persist in City code/databases. Eligible Apple devices/browsers can display Apple Pay through Stripe Elements when Apple Pay is enabled and the production domain is registered. If a resident opts to save a card, Stripe attaches the reusable PaymentMethod to the Customer; CLT# stores only Stripe identifiers and payment audit metadata.

The current `DevelopmentWaterBillingGateway` is intentionally a stub. Do not deploy it as an authoritative billing lookup. Replace it with Charlotte Water's CIS/billing API.

Server Stripe settings use placeholders in source. Put real values in environment variables or AWS Secrets Manager, never in Git.

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

Resident/public API includes service submissions/tracking, My Requests, water-bill lookup/payment, saved cards/payment history, Stripe webhook handling, and Auth0 login-audit ingestion.

Employee API supports queue queries across General, Police, Fire, and Airport plus request detail, assignment/status updates, and internal notes.

Recommended Auth0 employee API permissions:

```text
read:requests / write:requests  General municipal services
read:police   / write:police    Police Department
read:fire     / write:fire      Fire Department
read:airport  / write:airport   Airport
```

## About CLT# and AWS deployment

The resident portal includes a static About CLT# page documenting the complete application and data architecture plus the recommended AWS topology. The production design uses Route 53 and ACM; separate CloudFront distributions with private S3 origins for the two React applications; AWS WAF U.S.-only geo rules and managed controls; independent ECS Fargate services behind load balancers for the two .NET APIs; and separate private Multi-AZ RDS for PostgreSQL deployments for General, Police, Fire, and Airport data. Secrets Manager/KMS hold secrets and encryption keys; CloudWatch and CloudTrail provide application/infrastructure observability and audit trails.

Auth0 and Stripe remain external trust services. Auth0 owns identity and authentication; Stripe owns card/wallet payment credentials.

## Production requirements

Before production deployment: replace the development water-billing adapter; configure Auth0 applications/APIs and roles; deploy the Auth0 Post-Login Action; configure trusted GeoIP edge headers and block direct origin access; define IP/login audit retention and access policies; use managed secrets; enable and verify Stripe webhooks; register the Apple Pay domain; implement billing reconciliation/refunds and idempotency controls; run PCI/privacy/legal review; add immutable security audit logging; perform accessibility and security testing; and generate/review EF Core migrations.
