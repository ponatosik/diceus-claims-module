# DICEUS Claims Management Module

A Clean Architecture Claims FNOL/Reserve management backend built for the DICEUS Fullstack
Engineer take-home assessment. Implements First Notice of Loss intake, claim lifecycle
management, three-tier reserve authority/approval workflow, simulated GL posting, SLA
monitoring, and document management, per `REQUIREMENTS.md`.

## Current status

| Area | Status |
|---|---|
| Backend (.NET 9, Clean Architecture, CQRS) | **Done.** All 5 implementation slices (specs `001`–`005`) shipped, reviewed, and tested. 243/243 tests passing. |
| Frontend (Angular) | **Not started.** No `frontend/` directory exists yet — see `CLAUDE.md`. |
| Azure deployment (§19) | **Not done.** Deliberately scoped out of the backend effort — see [`wayfinder/map.md`](wayfinder/map.md)'s "Out of scope" section. The app runs locally only; see "Azure deployment" below for what reproducing a deployment would require. |
| CI/CD pipeline (§19/NFR-AZ-03) | **Not done.** No pipeline definition exists yet, for the same reason as Azure deployment. |

This README documents the backend as it stands today. Claims about a deployed URL, a frontend
walkthrough, or a CI/CD pipeline would be fabrications — none of those exist in this repository
yet, and this document does not pretend otherwise.

## Prerequisites

- [.NET SDK 9.0.200](https://dotnet.microsoft.com/download/dotnet/9.0) or later 9.x (pinned via `backend/global.json`)
- [Docker](https://www.docker.com/) — required to run `ClaimsModule.PersistenceTests` and
  `ClaimsModule.ApiIntegrationTests` (Testcontainers spins up real SQL Server + Azurite
  containers), and to run SQL Server locally for `dotnet run`. `ClaimsModule.UnitTests` has no
  Docker dependency.
- A SQL Server instance reachable at `localhost,1433` for local `dotnet run` (the quickest way to
  get one is the `docker run` command below — there is no committed `docker-compose.yml` yet).

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

**Not done in this phase.** `wayfinder/map.md` records this as a deliberate scope decision: Azure
deployment topology and CI/CD pipeline design (§19–20) were set aside as "a separate future
effort" so the backend implementation could proceed without re-litigating infrastructure choices
mid-slice. Nothing is deployed, and there is no live URL to share.

Reproducing a deployment that satisfies §19's minimum resource list would require:

- **Azure App Service** for the API (`ClaimsModule.API`), with the configuration keys above set
  as App Service application settings (double-underscore syntax).
- **Azure SQL Database** (or SQL Managed Instance) — two databases, mirroring the local setup: one
  for `ClaimsDatabase`, one for `HangfireDatabase`.
- **Azure Blob Storage** account, with `StorageProvider` set to `AzureBlob` and a real
  `ConnectionStrings:AzureBlobStorage` value.
- **Azure Static Web App** (or a second App Service) for the Angular frontend, once it exists.
- A basic GitHub Actions or Azure Pipelines definition that builds the backend, runs
  `dotnet ef database update` against the Azure SQL database, builds the frontend, and deploys
  both (NFR-AZ-03) — manually triggered is acceptable per the spec.

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
