# DICEUS Claims Management Module

A Clean Architecture Claims FNOL/Reserve management backend built for the DICEUS Fullstack
Engineer take-home assessment. Implements First Notice of Loss intake, claim lifecycle
management, three-tier reserve authority/approval workflow, simulated GL posting, SLA
monitoring, and document management, per `REQUIREMENTS.md`.

## Current status

| Area | Status |
|---|---|
| Backend (.NET 9, Clean Architecture, CQRS) | **Done.** Slices `001`–`006` shipped, reviewed, and tested (`006` = the gaps the frontend needs), plus ADR-0011 (reserve decreases/reversals). 292/292 tests passing. |
| Frontend (Angular 22) | **Done.** Specs `007`–`012`: shell + demo auth, claims dashboard, FNOL wizard, and Claim Detail (header, Overview, Parties, Reserves, Documents, Audit Log). 259 unit/component tests + 15 Playwright end-to-end tests against the docker-compose stack. |
| Local full stack (`docker-compose.yml`) | **Done.** SQL Server + migrations + API + nginx-served frontend in one command. |
| Azure deployment (§19) | **Defined, ready to run.** Bicep in `infra/`, a one-time bootstrap (`scripts/azure-bootstrap.sh`), then GitHub Actions provision and deploy everything — see "Azure deployment" below. Live URLs are listed there once deployed. |
| CI/CD pipeline (§19/NFR-AZ-03) | **Done.** `ci.yml` (every push: all backend + frontend tests, lint, Bicep check); `provision.yml` and `deploy.yml` (manual: Bicep; build → EF migrations → deploy API + frontend → smoke test). |

This README documents the system as it stands today. The deployment is fully defined and
runnable from this repo; the live URLs below are filled in only once it has actually been
deployed — until then they say so.

## Quick start (full stack in Docker)

```bash
docker compose up --build -d
```

- Frontend: <http://localhost:4200> — signed in as the handler demo user; switch user with "Act as"
  in the top bar.
- API / Swagger: <http://localhost:5218/swagger>

Compose starts SQL Server, creates the Hangfire database, applies EF Core migrations with a
one-shot migration bundle (the API never migrates itself on startup), then starts the API
(`DevAuth:Enabled=true`, local-filesystem document storage) and the frontend behind nginx, which
proxies `/api` and `/uploads` to the API so the browser sees one origin. `docker compose down -v`
resets all data.

## Prerequisites

- [.NET SDK 9.0.200](https://dotnet.microsoft.com/download/dotnet/9.0) or later 9.x (pinned via `backend/global.json`)
- [Docker](https://www.docker.com/) — required to run `ClaimsModule.PersistenceTests` and
  `ClaimsModule.ApiIntegrationTests` (Testcontainers spins up real SQL Server + Azurite
  containers), and to run SQL Server locally for `dotnet run`. `ClaimsModule.UnitTests` has no
  Docker dependency.
- A SQL Server instance reachable at `localhost,1433` for local `dotnet run` (the `docker run`
  command below, or `docker compose up -d sqlserver db-init migrate`).
- [Node.js 22 LTS](https://nodejs.org/) (pinned in `frontend/.nvmrc`) for frontend development.

## Local development setup

1. **Start a local SQL Server container** (first run pulls the image, ~5 minutes; subsequent runs are fast):

   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
     -p 1433:1433 --name claims-sql -d mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04
   ```

   This matches the connection string already committed in
   `backend/src/ClaimsModule.API/appsettings.Development.json` — no edits needed for local dev.

2. **Restore and build:**

   ```bash
   cd backend
   dotnet build
   ```

3. **Apply EF Core migrations** (creates the `ClaimsModule` database, schema, and seeds reference
   data — see "Database migrations" below):

   ```bash
   dotnet ef database update -p src/ClaimsModule.Persistence -s src/ClaimsModule.API
   ```

4. **Create the Hangfire stand-in database** (a second database on the same SQL Server instance,
   per ADR-0004 — Hangfire gets its own store, separate from the main application database):

   ```bash
   docker exec -it claims-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong!Passw0rd' -C \
     -Q "IF DB_ID('ClaimsModuleHangfire') IS NULL CREATE DATABASE ClaimsModuleHangfire;"
   ```

   Hangfire creates its own tables inside that database automatically on first run — no migration
   needed for it.

5. **Run the API:**

   ```bash
   dotnet run --project src/ClaimsModule.API
   ```

   Swagger UI is at `http://localhost:5000/swagger` (or whichever port the console output shows).
   Swagger is reachable in every environment, not just Development, per §20.3.

6. **Authenticate as a demo user.** There is no real identity provider (ADR-0007) — when
   `DevAuth:Enabled` is `true` (it is in `appsettings.Development.json`), `POST /api/dev/auth/token`
   mints a JWT for one of three fixed demo users (ADR-0010), taking the role from the seeded
   `Users` table:

   ```bash
   curl -X POST http://localhost:5000/api/dev/auth/token \
     -H "Content-Type: application/json" \
     -d '{"userId": "00000000-0000-0000-0003-000000000001"}'
   ```

   | `userId` | Display name | Role |
   |---|---|---|
   | `00000000-0000-0000-0003-000000000001` | Alex Morgan | `handler` |
   | `00000000-0000-0000-0003-000000000002` | Priya Shah | `supervisor` |
   | `00000000-0000-0000-0003-000000000003` | Daniel Okafor | `manager` |

   Use the returned `accessToken` as a `Bearer` token — Swagger's "Authorize" button accepts it
   directly. The ids are stable, so submitter-only retract and self-approval rejection (BR-R-03)
   behave consistently across sessions. With `DevAuth:Enabled` off (the default) the endpoint
   returns `404` — it must never be mistaken for production-appropriate auth.

### Running the tests

```bash
cd backend
dotnet test tests/ClaimsModule.UnitTests      # fast loop, no Docker needed — 98 tests
dotnet test                                     # everything, requires Docker — 243 tests total
```

`ClaimsModule.PersistenceTests` and `ClaimsModule.ApiIntegrationTests` spin up their own
Testcontainers (SQL Server + Azurite) independently of the `dotnet run` setup above — they don't
need the `claims-sql` container from step 1.

## Environment variables / configuration

All configuration lives in `backend/src/ClaimsModule.API/appsettings.json` (defaults, safe to
commit — no real secrets) and `appsettings.Development.json` (local dev overrides). In a real
deployment these would be supplied as environment variables or Azure App Service configuration,
using the standard ASP.NET Core double-underscore convention (e.g.
`ConnectionStrings__ClaimsDatabase`).

| Key | Purpose | Example value |
|---|---|---|
| `ConnectionStrings:ClaimsDatabase` | Main application database | `Server=localhost,1433;Database=ClaimsModule;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;` |
| `ConnectionStrings:HangfireDatabase` | Hangfire's own store (ADR-0004) | `Server=localhost,1433;Database=ClaimsModuleHangfire;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;` |
| `ConnectionStrings:AzureBlobStorage` | Azure Blob Storage connection string | `UseDevelopmentStorage=true` (Azurite), or a real Azure Storage connection string |
| `StorageProvider` | `AzureBlob` or `LocalFileSystem` (§7.4/§13, BR-D-03) | `LocalFileSystem` — the committed dev default, since no real Azure Storage account is configured locally |
| `LocalStorage:RootPath` | Where documents are written when `StorageProvider` is `LocalFileSystem` | `uploads` |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:SigningKey` | Self-hosted test JWT issuer config (ADR-0007) | see `appsettings.Development.json` — dev-only, not a real secret |
| `DevAuth:Enabled` | Turns on the demo token issuer `POST /api/dev/auth/token` (ADR-0010). Demo/dev only | `false` (default); `true` in `appsettings.Development.json` |
| `Cors:AllowedOrigins` | Browser origins allowed to call the API (the deployed Angular frontend). No wildcard; local dev uses the `ng serve` proxy instead | `["https://<frontend>.azurestaticapps.net"]`; empty by default |
| `Hangfire:StartServer` | Set to `false` to register jobs without running a worker (used by the test fixture) | `true` (default) |

**Fallback behavior worth knowing:** even if `StorageProvider` is set to `AzureBlob`, the app
falls back to `LocalFileSystem` automatically whenever `ConnectionStrings:AzureBlobStorage` is
blank (BR-D-03) — this is why the committed `appsettings.json` (blank connection string) never
500s on a document upload even without real Azure credentials configured.

## Database migrations

Migrations live in `backend/src/ClaimsModule.Persistence/Migrations/`. To apply them:

```bash
cd backend
dotnet ef database update -p src/ClaimsModule.Persistence -s src/ClaimsModule.API
```

To add a new migration after a model change:

```bash
dotnet ef migrations add <Name> -p src/ClaimsModule.Persistence -s src/ClaimsModule.API
```

Reference data (cause-of-loss codes, simulated policies — §5.5/§5.6) is seeded by the
`AddReferenceDataWithSeeding` migration itself (`HasData` calls in the EF Core model), so applying
migrations is sufficient — there is no separate seed script to run.

## Azure deployment

Everything is defined in this repo (spec `013`, [ADR-0012](docs/adr/0012-free-tier-azure-deployment.md)) and
runs from GitHub Actions; the only manual part is a one-time bootstrap.

**Resources** (one resource group, `rg-diceus-claims`, West Europe — all on free or capped tiers to stay
within a $2/month budget):

| Resource | Tier | Notes |
|---|---|---|
| App Service (Linux) — the API, Swagger at `/swagger` | **F1 Free** | Sleeps after ~20 idle minutes; the first request after that takes ~10–20 s (cold start). Hangfire jobs (GL posting, SLA check) run while it's awake. |
| Static Web App — the Angular frontend | **Free** | Calls the API cross-origin; `assets/config.json` is written at deploy time with the API URL. |
| Azure SQL — `ClaimsModule` + `ClaimsModuleHangfire` | **Free offer** (serverless) | 100,000 vCore-seconds + 32 GB per database per month (≈55 awake hours at the 0.5 vCore minimum). If a database exhausts its allowance it **pauses instead of billing — and the app is down until the 1st of the next month**. The API sleeping when idle keeps usage far below this for a review. |
| Storage account — document blobs | Standard LRS, Hot | Downloads are 1-hour SAS URLs. |
| Key Vault | Standard | Every secret (SQL password, connection strings, blob connection string, JWT signing key); the API reads them via Key Vault references with its managed identity. |
| Budget | $2 / month | Emails at 50% and 100% of actual and 100% of forecast spend. |

No Application Insights, Log Analytics or Defender plans (they would cost money); use the App Service log
stream for diagnostics. SQL accepts connections only from the API's outbound IPs (and, during a deploy, the
runner's IP) — no "allow all Azure services" rule.

**Reproducing the deployment**

1. Run `scripts/azure-bootstrap.sh` locally (needs `gh`, logged in). It walks you through a block to paste into
   [Azure Cloud Shell](https://shell.azure.com) that creates the resource group and a GitHub-only deploy identity
   (OIDC federated credential — no stored secrets), then sets the repo variables `AZURE_CLIENT_ID`,
   `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and `BUDGET_ALERT_EMAIL`, and can trigger the two workflows below.
2. **Provision Azure** (`provision.yml`, manual): `what-if`, then deploys `infra/main.bicep`. The SQL password and
   JWT key are generated on the first run and reused afterwards.
3. **Deploy** (`deploy.yml`, manual): builds the API and an EF migration bundle, applies migrations through a
   firewall rule opened only for the runner (and removed afterwards), deploys the API, builds and deploys the
   frontend, runs `deploy/smoke-test.sh`, and — with **seedDemoClaims** — creates six demo claims through the API
   (`deploy/seed-demo-claims.mjs`, skipped if any claims exist — so a seed that fails part-way is not
   completed by re-running it).

**Caveats.** Key Vault keeps deleted vaults for 7 days (soft delete): if you delete the resource group, wait
7 days or purge the vault (`az keyvault purge --name <vault>`) before provisioning again. The deploy identity
can create role assignments, but only for the *Key Vault Secrets User* role (an RBAC condition).

**Live URLs** — filled in after the first deployment (also printed in each workflow run's summary):

- Frontend: _pending first deploy_
- API / Swagger: _pending first deploy_

### Test credentials

There are no passwords: the deployed app uses the three fixed demo users (ADR-0010). Pick one with **Act as** in
the top bar — the app signs in as that user with a real, server-validated JWT.

| User | Role | Can |
|---|---|---|
| Alex Morgan | handler | Log claims, manage parties and documents, submit reserves (auto-approved up to $10,000) |
| Priya Shah | supervisor | Everything a handler can, plus approve/reject reserves up to $100,000 and reopen claims |
| Daniel Okafor | manager | Everything, including reserves above $100,000 and the $10M aggregate-limit override |

To try the approval flow: as Alex, add a $25,000 reserve; switch to Priya and approve it — the GL posting
badge moves from Pending to Posted within about a minute.

## Feature walkthrough (backend)

Everything below is implemented and covered by the integration test suite
(`backend/tests/ClaimsModule.ApiIntegrationTests`). Full endpoint list in Swagger once the API is
running; high points:

- **FNOL intake** (`POST /api/claims`) — creates a `Draft` claim with a loss event, at least one
  claimant party, optional risk objects, and an optional initial reserve, all in one transaction.
  Claim numbers (`CLM-{YYYY}-{7-digit-seq}`) are generated via a DB-level SQL `SEQUENCE`, so
  concurrent submissions never collide (ADR-0001). `POST /api/claims/validate` runs the same
  validation without creating anything, for the FNOL form's per-step feedback.
- **Claim reads & lifecycle** (`GET /api/claims`, `GET /api/claims/{id}`,
  `PUT /api/claims/{id}/status`, `GET /api/claims/{id}/status/preflight?target=`,
  `PATCH /api/claims/{id}/notes`) — paginated list filterable by several statuses
  (`?status=Open&status=Closed`), handler name (`?assignedHandler=`), dates, cause of loss and
  search; full detail view; and a strict status-transition state machine (§4.2) that rejects
  anything not in the transition table with a 422 and the list of valid next statuses. The
  pre-flight endpoint evaluates the same blocking conditions (e.g. CC-01–CC-04 for Closed) without
  changing anything, for the frontend's closure checklist.
- **Party management** (`POST`/`DELETE /api/claims/{id}/parties/...`) — add/soft-remove parties.
- **Reserve management** (`POST /api/claims/{id}/reserves`, `/approve`, `/reject`, `/retract`,
  `/retry-posting`, `/reserves/override`) — the three-tier authority workflow (≤$10,000 auto-approves; higher
  amounts need Supervisor/Manager approval), self-approval rejection (BR-R-03), and an
  append-only `ReserveHistory` ledger — `CurrentAmount` is never updated directly, only ever moved
  by a new history row reaching an approved/posted state (ADR-0002).
- **Background jobs** (Hangfire, against its own SQL Server store per ADR-0004) — an outbox
  dispatcher bridges approved reserves into enqueued GL-posting jobs; the GL posting job simulates
  the ledger entry and is idempotent via a deterministic key (ADR-0005); a recurring SLA-monitoring
  job flags claims stuck in `Draft`/`Open` past the 48-hour window.
- **Document management** (`POST`/`GET /api/claims/{id}/documents`) — uploads go through
  `IStorageService` (Azure Blob or local-filesystem fallback, BR-D-03), filenames are sanitised
  against path traversal and de-duplicated, and downloads are always short-lived signed/served
  URLs — document bytes are never proxied through the API.
- **Audit trail** (`GET /api/claims/{id}/audit`) — every write funnels through a centralized
  `IAuditLogService`; `CorrelationId` from the originating HTTP request propagates to every audit
  entry written during that request, including from background jobs.

See `ARCHITECTURE.md` for how these fit together, and `docs/adr/0001`–`0009` for the reasoning
behind each hard-to-reverse decision (`0010` covers the demo-user token issuer).

## Repository layout

See `CLAUDE.md` for the full repository layout and cross-cutting architecture notes, and
`REQUIREMENTS.md` for the authoritative, traceability-tagged requirements spec this implementation
follows.
