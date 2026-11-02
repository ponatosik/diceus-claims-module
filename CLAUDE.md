# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

**Backend slices A–F (`specs/001`–`006`) are implemented. Frontend slice G (`specs/007` — shell, demo auth, errors, API layer, docker-compose) is implemented**; `specs/008`–`012` (slices H–L) are next — 008/009/010 can run in parallel, 011/012 after 010.

- `REQUIREMENTS.md` — the single source of truth. A consolidated, non-summarized requirements spec compiled from the two source PDFs (`Claims_Module_Candidate_Specification.pdf`, `DICEUS_Fullstack_Technical_Assessment.pdf`). Read this file, not the PDFs, for implementation detail — it reproduces every business rule, data entity, API contract, and constraint verbatim with traceability IDs (`BR-*`, `CC-*`, `FR-*`, `NFR-*`).
- `wayfinder/map.md` — the backend implementation decision log (local-markdown issue tracker; see `wayfinder/TRACKER.md` for the convention). Resolves the architecture choices `REQUIREMENTS.md` deliberately leaves open. **Check this before making a call `REQUIREMENTS.md` doesn't pin down** — it's likely already decided here, with the full reasoning in `docs/adr/`.
- `GLOSSARY.md` — canonical domain terms (Authority Tier, Submitter, Pending Amount, In-force, Blocking Condition, Pre-flight Check, Aggregate Limit Override, Demo User). Use these words in code, specs and UI copy.
- `docs/adr/0010` — dev token issuer gated by `DevAuth:Enabled` + three fixed Demo Users (amends ADR-0007); the basis for the frontend's mock auth.
- `docs/adr/0001`–`0009` — ADRs for the hard-to-reverse backend decisions (claim number generation, reserve balance persistence, repository/Unit-of-Work shape, domain events + outbox, GL posting idempotency, exception-to-status-code mapping, JWT auth strategy, validation-severity pipeline + business-rule error shape, testing strategy). Each is 1–3 paragraphs; read the relevant one before touching that area.
- One wayfinder ticket is still open (11 — correlation-id for background/outbox-dispatched audit entries). Check `wayfinder/map.md`'s frontier before assuming it's settled; everything else on the backend decision log is closed.

Before writing code, re-read `REQUIREMENTS.md` section by section as needed — it is long (25 sections) and organized so each section is self-contained. Do not re-derive requirements from memory across sessions; `REQUIREMENTS.md` and the wayfinder decision log are authoritative over any prior summary.

This is an enterprise take-home assignment (DICEUS Fullstack Engineer assessment), not a tutorial/learning exercise — treat architectural decisions, validation completeness, and audit/traceability with production-grade rigor, since the evaluation explicitly weights "Engineering Quality & Architecture" at 35% and "Domain & Product Thinking" at 15% (`REQUIREMENTS.md` §22).

## Repository Layout

Monorepo:

```
/backend/
  global.json                      pins SDK to 9.0.200
  Directory.Build.props            shared TargetFramework/Nullable/LangVersion for every project
  Directory.Packages.props         central NuGet package version management — add new packages here, never inline a Version= on a PackageReference
  ClaimsModule.sln
  src/
    ClaimsModule.Domain           entities, value objects, domain events, enums — no dependencies
    ClaimsModule.Application      MediatR commands/queries, FluentValidation validators, DTOs, interfaces, AutoMapper profiles — depends on Domain; owns its own DI registration (ApplicationServiceCollectionExtensions.AddApplication())
    ClaimsModule.Infrastructure   Azure Blob / local filesystem storage, Hangfire job definitions — depends on Application
    ClaimsModule.Persistence      EF Core DbContext (ClaimsDbContext, currently no DbSets), migrations, repositories, Unit of Work — depends on Application; owns its own DI registration (PersistenceServiceCollectionExtensions.AddPersistence())
    ClaimsModule.API              controllers, middleware, startup/config — depends on Application, Infrastructure, Persistence; Program.cs is deliberately thin, wiring via each layer's AddXxx() extension
  tests/
    ClaimsModule.UnitTests          xUnit, no I/O, no containers. Domain logic, FluentValidation validators, MediatR handlers with mocked repositories, plus a NetArchTest.Rules architecture-fitness suite (ArchitectureTests.cs) enforcing the dependency rules above at build time.
    ClaimsModule.PersistenceTests   xUnit + Testcontainers (SQL Server only). EF Core configurations/migrations/query filters — no HTTP, no MediatR. DatabaseFixture is an xUnit collection fixture (one container per assembly run, not per test); DatabaseFixture.ResetAsync() uses Respawn for inter-test isolation.
    ClaimsModule.ApiIntegrationTests   xUnit + Testcontainers (SQL Server with 2 DBs — main + a Hangfire-store stand-in per ADR-0004 — plus Azurite). ApiFixture extends WebApplicationFactory<Program> against real containers; full HTTP request/response cycles through the real MediatR pipeline. GL posting idempotency tests (once the feature exists) belong here, not a separate project, per ADR-0009.
/frontend/           Angular 22 workspace — standalone components, zoneless, signals; Vitest unit tests, Playwright e2e (e2e/)
  src/app/core/        auth (MockAuthService, interceptor, permissions), errors (ApiError, error interceptor, snackbars), runtime config, shell, not-found
  src/app/shared/      domain helpers (authorityTier, isInForce, claim status), forms (applyServerErrors), ui (StatusBadge, Skeleton, PendingTracker)
  src/app/api/         typed DTO models + one service per resource — the only place HttpClient is used (§11.4)
  src/app/features/    claims-list, fnol-intake, claim-detail — one lazy route bundle each (*.routes.ts)
docker-compose.yml   SQL Server + db-init (Hangfire DB) + migrate (EF bundle) + API + nginx-served frontend
```

Dependency direction is strict Clean Architecture: `API → Application → Domain`, with `Infrastructure`/`Persistence` implementing interfaces defined in `Application`/`Domain`. Nothing in `Domain` or `Application` may reference EF Core, Hangfire, or Azure SDK types directly — those live behind interfaces (`IAuditLogService`, `IStorageService`, repository interfaces) implemented in `Persistence`/`Infrastructure`. `ClaimsModule.UnitTests/ArchitectureTests.cs` enforces this automatically — a build failure there means a genuine layering violation, not a flaky test.

**Central package management is on** (`ManagePackageVersionsCentrally=true`). Adding a package is `dotnet add <project> package <Name>` as usual — the CLI inserts the version into `Directory.Packages.props` automatically; never add an inline `Version=` to a `PackageReference` in a `.csproj`. EF Core and other ASP.NET Core shared-framework packages (`Microsoft.AspNetCore.Mvc.Testing`, etc.) must be pinned to the `9.0.x` line explicitly (`--version 9.0.22` or later 9.x) — NuGet's default "latest" resolution will happily grab a .NET 10-only package and fail to restore. Swashbuckle.AspNetCore is the sole OpenAPI/Swagger package — don't add `Microsoft.AspNetCore.OpenApi` alongside it; the two ship incompatible `Microsoft.OpenApi` model versions and the app throws `TypeLoadException` at startup if both are present. Testcontainers builder constructors (`new MsSqlBuilder()`, `new AzuriteBuilder()`) are obsolete without an explicit image argument — pass the image tag explicitly (see `DatabaseFixture`/`ApiFixture` for the pinned tags in use) rather than relying on the deprecated default-image constants.

**Running the Testcontainers-backed projects requires Docker.** `ClaimsModule.PersistenceTests` and `ClaimsModule.ApiIntegrationTests` will hang/fail without a reachable Docker daemon; `ClaimsModule.UnitTests` has no such requirement and should stay the fast, no-Docker-needed feedback loop. First run of each pulls its container images (SQL Server ~5 min, Azurite much faster) — subsequent runs are fast since Docker caches the layers.

## Commands

```bash
# Backend (.NET 9 / C# 13) — run from /backend
dotnet build
dotnet test                                                    # all tests (requires Docker — PersistenceTests/ApiIntegrationTests use Testcontainers)
dotnet test tests/ClaimsModule.UnitTests                       # fast loop, no Docker needed
dotnet test --filter "FullyQualifiedName~ClassOrMethodName"    # single test/class
dotnet run --project src/ClaimsModule.API                      # Swagger UI at /swagger/index.html — must stay reachable outside Development too (§20.3)

# EF Core migrations — not usable yet, ClaimsDbContext has no DbSets until entities exist
dotnet ef migrations add <Name> -p src/ClaimsModule.Persistence -s src/ClaimsModule.API
dotnet ef database update -p src/ClaimsModule.Persistence -s src/ClaimsModule.API

# Frontend (Angular 22, Node 22) — run from /frontend
npm install
npx ng serve                 # http://localhost:4200, proxies /api + /uploads to the API on :5218 (proxy.conf.json)
npx ng build
npx ng test --watch=false    # Vitest unit tests, no API needed
npx ng lint
npm run format:check
npm run e2e                  # Playwright against the docker-compose stack (start it first: docker compose up --build -d)

# Full stack — run from repo root
docker compose up --build -d # frontend :4200, API :5218
```

## Architecture Notes (cross-cutting, non-obvious — read before implementing)

These are the design decisions that span multiple sections of `REQUIREMENTS.md` and are easy to get wrong if only one section is read in isolation.

- **CQRS is mandatory, not optional.** Every state-changing operation is a MediatR `Command`; every read is a MediatR `Query`. FluentValidation validators must be wired as a **MediatR pipeline behavior**, not just controller-level `[ApiController]` model validation — reviewers explicitly check this (§18.1).
- **Reserves use event sourcing, not CRUD.** `ClaimReserveComponents.CurrentAmount` is a computed projection — it is never directly updated. Every change is an **insert** into `ReserveHistory`; the current balance is the sum of posted/approved transactions for that component (§6.6, §9.5–9.6). Getting this wrong (updating `CurrentAmount` directly) breaks the audit trail requirement.
- **Reserve authority thresholds gate GL posting, not reserve creation.** ≤$10,000 auto-approves and immediately enqueues the Hangfire GL posting job; >$10,000 creates a `PendingApproval` record and the GL job is deliberately *not* enqueued until a Supervisor/Manager approves it (§6.3–6.4). A `PendingApproval` reserve cannot be edited — it must be retracted (→ `Cancelled`) and resubmitted.
- **Self-approval must be rejected at the domain/application layer**, comparing submitter `userId` to approver `userId` — this is a named business rule (`BR-R-03`) with its own 422 error, not an incidental check.
- **Hangfire jobs are idempotent via a deterministic key**: `Reserve:{ReserveId}:Change:{ChangeSequence}` for GL posting (§6.5, §12.1). Running a job twice must be a no-op on the second run — check `ReserveHistory.PostingStatus` / `IdempotencyKey` before writing. The SLA monitoring job (§12.2) has its own idempotency rule: only emit a new `SLA_BREACH_DETECTED` entry if 24+ hours have passed since the last one for that claim.
- **Audit logging is centralized and append-only.** All writes to `ClaimAuditLog` must go through a dedicated `IAuditLogService` — no handler writes to the audit table directly via `DbContext` (`AUD-02`). The table must have no UPDATE/DELETE grants. `CorrelationId` from the HTTP request must propagate to every audit entry written during that request (`AUD-04`).
- **Claim numbers are generated atomically** (`CLM-{YYYY}-{7-digit-seq}`) using a DB-level atomic increment (sequence or counter table with optimistic concurrency) — never read-then-increment in application code, to avoid duplicates under concurrent FNOL submissions (§5.3).
- **Status transitions are a strict state machine.** Any transition not explicitly listed in the transition table (§4.2) is invalid and must return HTTP 422 with the list of valid next statuses — do not implement this as free-form status assignment.
- **Role enforcement is required in two independent places**: backend endpoint authorization (reserve approval) *and* frontend UI gating (approve/reject buttons hidden for non-supervisor/manager) — implementing only one is explicitly called out as insufficient (§3, `FR-ROLE-01`).
- **Storage is provider-abstracted.** `IStorageService` with `AzureBlobStorageService` and `LocalFileSystemStorageService` implementations, selected via `appsettings.json` (`StorageProvider: AzureBlob | LocalFileSystem`) — Azure Blob is primary, local filesystem is the required dev fallback (§7.4, §13). Document bytes are never proxied through the API; only short-lived (1-hour) SAS URLs are returned.
- **Tenant isolation and soft delete are global, not per-query.** Every business table carries `OrganisationId` and `IsDeleted`; EF Core global query filters must enforce both (`modelBuilder.Entity<T>().HasQueryFilter(...)`), not ad-hoc `WHERE` clauses in individual queries (§16.2).
- **Data type conventions are non-negotiable and apply to every table**: `UNIQUEIDENTIFIER` PKs via `NEWSEQUENTIALID()`, `DECIMAL(19,4)` for all money (never `float`/`money`), `DATETIMEOFFSET(7)` UTC timestamps, full audit columns (`CreatedAt`/`UpdatedAt`/`UserCreated`/`UserModified`), `RowVer ROWVERSION` on aggregate roots (`Claims`, `ClaimReserveComponents`) for optimistic concurrency (§16.1). Configure via EF Core Fluent API (`IEntityTypeConfiguration<T>`) — not data annotations.
- **Frontend talks to the API only through a typed Angular service layer** — no direct `HttpClient` calls inside components (§11.4). A mock auth service + HTTP interceptor simulates JWT Bearer auth and role switching (handler/supervisor/manager) for testing role-gated UI.
- **Policy domain is simulated, not real.** There is no real Policy module — a seeded in-memory/DB table (§5.5) stands in for it. Don't build out policy issuance/coverage logic beyond what's needed for FNOL lookup and the in-force date check.

## Required Deliverable Documents (in addition to code)

Per `REQUIREMENTS.md` §20, the finished submission must also include (create these as the corresponding implementation phases complete, not upfront):
- `README.md` — prerequisites, local setup, env vars, migration steps, Azure deployment description, feature walkthrough
- `ARCHITECTURE.md` — structure, data model, CQRS flow, domain events, Hangfire job design, Azure architecture, key tradeoffs
- `AI-WORKFLOW.md` — AI tools used, prompting strategy, ≥3 representative prompts, ≥2 examples of AI output that was wrong and corrected, honest assessment of where AI helped most
- AI interaction history export (Claude share link / exported conversation)

These are evaluation deliverables, not internal docs — write them for the DICEUS reviewers reading them during the live technical review (§23), not as generic documentation.
