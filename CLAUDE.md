# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

**No code exists yet.** This repository currently contains only the requirements baseline:

- `REQUIREMENTS.md` — the single source of truth. A consolidated, non-summarized requirements spec compiled from the two source PDFs (`Claims_Module_Candidate_Specification.pdf`, `DICEUS_Fullstack_Technical_Assessment.pdf`). Read this file, not the PDFs, for implementation detail — it reproduces every business rule, data entity, API contract, and constraint verbatim with traceability IDs (`BR-*`, `CC-*`, `FR-*`, `NFR-*`).
- The two source PDFs, kept for reference only.

Before writing code, re-read `REQUIREMENTS.md` section by section as needed — it is long (25 sections) and organized so each section is self-contained. Do not re-derive requirements from memory across sessions; this doc is authoritative over any prior summary.

This is an enterprise take-home assignment (DICEUS Fullstack Engineer assessment), not a tutorial/learning exercise — treat architectural decisions, validation completeness, and audit/traceability with production-grade rigor, since the evaluation explicitly weights "Engineering Quality & Architecture" at 35% and "Domain & Product Thinking" at 15% (`REQUIREMENTS.md` §22).

## Planned Repository Layout

Monorepo, two top-level directories (not yet created):

```
/backend/            .NET 9 solution, Clean Architecture, 5 projects:
  ClaimsModule.Domain           entities, value objects, domain events, enums
  ClaimsModule.Application      MediatR commands/queries, FluentValidation validators, DTOs, interfaces, AutoMapper profiles
  ClaimsModule.Infrastructure   Azure Blob / local filesystem storage, Hangfire job definitions
  ClaimsModule.Persistence      EF Core DbContext, migrations, repositories, Unit of Work
  ClaimsModule.API              controllers, middleware, startup/config
/frontend/           Angular 18+ workspace (Angular Material, Reactive Forms)
docker-compose.yml   local dev: SQL Server + API + frontend (optional per spec, but planned)
```

Dependency direction is strict Clean Architecture: `API → Application → Domain`, with `Infrastructure`/`Persistence` implementing interfaces defined in `Application`/`Domain`. Nothing in `Domain` or `Application` may reference EF Core, Hangfire, or Azure SDK types directly — those live behind interfaces (`IAuditLogService`, `IStorageService`, repository interfaces) implemented in `Persistence`/`Infrastructure`.

## Commands (once scaffolded)

These are the commands this stack implies (`REQUIREMENTS.md` §17.4) — use them once the solution/workspace exist; update this section with real, verified commands as soon as scaffolding lands.

```bash
# Backend (.NET 9 / C# 13)
cd backend
dotnet build
dotnet test                                   # all tests
dotnet test --filter "FullyQualifiedName~X"   # single test
dotnet ef migrations add <Name> -p ClaimsModule.Persistence -s ClaimsModule.API
dotnet ef database update -p ClaimsModule.Persistence -s ClaimsModule.API
dotnet run --project ClaimsModule.API         # Swagger/OpenAPI must be reachable

# Frontend (Angular 18+)
cd frontend
npm install
ng serve
ng build
ng test
ng lint
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
