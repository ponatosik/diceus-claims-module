# Architecture

This document covers the backend as implemented (all 5 slices, specs `001`–`005`). There is no
frontend yet, and nothing is deployed to Azure — see `README.md`'s status table for why, and
"Azure architecture" below for the intended target shape.

## Solution structure

Strict Clean Architecture, enforced at build time (not just by convention — see "How this is
enforced" below):

```
API  ──depends on──▶  Application  ◀──depends on──  Infrastructure
                            ▲                              ▲
                            └──────depends on───────  Persistence
                            ▲
                         Domain
```

```
backend/src/
  ClaimsModule.Domain           entities, enums — zero dependencies on anything
    Entities/                     Claim, LossEvent, ClaimParty, ClaimRiskObject,
                                   ClaimReserveComponent, ReserveHistory, ClaimDocument,
                                   ClaimAuditLog, OutboxMessage, Policy, CauseOfLossCode,
                                   plus AppendOnlyEntity/AuditableEntity base classes
    Enums/                         ClaimStatus, ClaimSeverity, PartyRole, PartyType, AssetType,
                                   ReserveComponentType/Status/ApprovalStatus/PostingStatus/
                                   TransactionType, PerilCategory, PolicyStatus

  ClaimsModule.Application      MediatR commands/queries, validators, DTOs — depends on Domain only
    Claims/<Verb>/                 one folder per command: Command + Handler + Validator
                                   (CreateClaim, OpenReserve, ApproveReserve, RejectReserve,
                                   RetractReserve, SetReserveOverride, TransitionClaimStatus,
                                   AddClaimParty, RemoveClaimParty, UploadClaimDocument)
    Claims/<Noun>Query.cs          single-file query+handler bundles (GetClaimDetailQuery,
                                   ListClaimsQuery, ListClaimReservesQuery, ListClaimDocumentsQuery,
                                   GetClaimAuditLogQuery, ValidateClaimQuery, ...)
    Claims/Dtos/                   response DTOs + AutoMapper-adjacent mapping helpers
    Policies/                      GetPolicyCoverageQuery, SearchPoliciesQuery (simulated policy data)
    Common/                        IAuditLogService, ICurrentUserService, IUnitOfWork,
                                   IClaimRepository / IClaimReserveComponentRepository /
                                   IClaimDocumentRepository / IPolicyRepository /
                                   ICauseOfLossCodeRepository / IOutboxRepository /
                                   IClaimNumberGenerator / IStorageService (all interfaces —
                                   implementations live in Persistence/Infrastructure),
                                   Behaviors/ValidationBehavior, Exceptions/, Mapping/

  ClaimsModule.Persistence      EF Core — depends on Application (implements its interfaces)
    ClaimsDbContext.cs, UnitOfWork.cs, SeedData.cs, SqlSequenceClaimNumberGenerator.cs,
    AuditLogService.cs, AuditableEntitySaveChangesInterceptor.cs
    Configurations/                IEntityTypeConfiguration<T> per entity (Fluent API only, no
                                   data annotations — §16.1)
    Repositories/                  ClaimRepository, ClaimReserveComponentRepository,
                                   ClaimDocumentRepository, PolicyRepository,
                                   CauseOfLossCodeRepository, OutboxRepository
    Migrations/

  ClaimsModule.Infrastructure    background jobs + storage — depends on Application
    BackgroundJobs/                OutboxDispatcherJob, PostGLReserveChangeJob,
                                   GLPostingFailureFilter/Handler, SlaMonitoringJob
    Storage/                       AzureBlobStorageService, LocalFileSystemStorageService

  ClaimsModule.API              controllers, middleware, startup — depends on all of the above
    Controllers/                   ClaimsController, PoliciesController, ReferenceController,
                                   DevAuthController
    Authentication/                JWT wiring, the self-hosted test token issuer
    Middleware/                    ExceptionHandlingMiddleware
    Program.cs                     thin — wiring only, via each layer's AddXxx() extension
```

### How this is enforced

`ClaimsModule.UnitTests/ArchitectureTests.cs` uses `NetArchTest.Rules` to assert, at build/test
time, that the `Domain` assembly has no dependency on `Microsoft.EntityFrameworkCore`, `Hangfire`,
`Azure`, `MediatR`, or `FluentValidation`, and that the `Application` assembly has no dependency on
`ClaimsModule.Persistence`, `ClaimsModule.Infrastructure`, or `ClaimsModule.API`. A layering
violation fails the build, not a code review comment.

## Data model

Core aggregate: **`Claim`** (the aggregate root — `ClaimId`, `ClaimNumber`, `PolicyId` (nullable
FK to the simulated `Policy` table), `Status`, `Severity`, `ReportedDate`, `AssignedHandlerId`,
closure fields, `ReserveOverrideApproved`, `RowVer` for optimistic concurrency).

```
Claim 1───1 LossEvent              (loss date, cause-of-loss code, estimated amount, ...)
Claim 1───* ClaimParty             (Claimant/Insured/ThirdParty/Witness/Attorney; soft-removable)
Claim 1───* ClaimRiskObject        (damaged assets; one may be IsPrimary)
Claim 1───* ClaimReserveComponent  (Indemnity / Expense / ALAE / SubrogationRecoverable)
Claim 1───* ClaimDocument          (uploaded evidence; stored via IStorageService)
Claim 1───* ClaimAuditLog          (append-only; every write in the system funnels here)

ClaimReserveComponent 1───* ReserveHistory   (every change is an INSERT, never an UPDATE)
```

**`ClaimReserveComponent.CurrentAmount` is a persisted column, not a live `SUM()`** (ADR-0002) —
but it is never written directly by a handler. Every reserve change inserts a `ReserveHistory`
row carrying `PreviousBalance`/`NewBalance`/`Amount`, and `CurrentAmount` is only advanced in the
same transaction once that row reaches an auto-approved or supervisor/manager-approved state.
`ReserveHistory` also carries the approval workflow fields (`ApprovalStatus`,
`ApprovedByUserId`/`ApprovedAt`, `RejectedByUserId`/`RejectedAt`/`RejectionReason`), the GL-posting
idempotency fields (`PostingStatus`, `PostingJobId`, `IdempotencyKey` — unique-constrained,
ADR-0005), and `ChangeSequence` (feeds the deterministic idempotency key,
`Reserve:{ReserveId}:Change:{ChangeSequence}`).

Every business table carries `OrganisationId` + `IsDeleted` (global EF Core query filters, §16.2),
full audit columns (`CreatedAt`/`UpdatedAt`/`UserCreated`/`UserModified`, stamped automatically by
`AuditableEntitySaveChangesInterceptor` — handlers never set these themselves), and a
`DATETIMEOFFSET(7)` UTC convention. `Claim` and `ClaimReserveComponent` additionally carry a
`RowVer ROWVERSION` column for optimistic concurrency (§16.1) — this is what turns a lost-update
race into a `409` via `DbUpdateConcurrencyException` rather than a silent overwrite.

## CQRS flow

Every state change is a MediatR `Command`; every read is a MediatR `Query`. Concrete example —
`POST /api/claims`:

1. **`ClaimsController.CreateClaim`** maps the request body to a `CreateClaimCommand` and sends it
   through `IMediator`. The controller has no business logic — it only translates HTTP ⇄ MediatR.
2. **`ValidationBehavior<TRequest, TResponse>`**, a MediatR pipeline behavior registered once in
   `AddApplication()` (not per-controller `[ApiController]` model validation), runs every
   registered `IValidator<CreateClaimCommand>` — here, `CreateClaimCommandValidator`. Per
   ADR-0008, only `Severity.Error` failures throw (→ `422` with a field-keyed `errors` dict);
   `Severity.Warning` failures (e.g. BR-C-02's "loss date outside policy period") are collected
   onto the command via `IHasValidationWarnings` instead of blocking it.
3. **`CreateClaimCommandHandler`** (`ClaimsModule.Application/Claims/CreateClaim/`) does the real
   work: generates a claim number via `IClaimNumberGenerator` (a DB-level `SEQUENCE`, ADR-0001 —
   never read-then-increment in application code), resolves the optional policy via
   `IPolicyRepository`, builds the `Claim` aggregate with its `LossEvent`/`ClaimParty`/
   `ClaimRiskObject` children, calls `IAuditLogService.LogAsync` for `CLAIM_CREATED` (and one
   `VALIDATION_ISSUE_ADDED` entry per attached warning), then `IClaimRepository.CommitChangesAsync`.
4. **`IClaimRepository`** (interface in `Application/Common`, implemented in
   `Persistence/Repositories/ClaimRepository.cs`) adds the aggregate to the tracked `DbContext`.
   `CommitChangesAsync()` is a thin pass-through to a shared `IUnitOfWork`, which owns the actual
   `SaveChangesAsync` call (ADR-0003) — this is what lets the claim, its children, and the audit
   log entries commit atomically in one transaction, and what every other multi-repository handler
   in the codebase relies on for the same reason.
5. The handler maps the persisted `Claim` to a `ClaimDto` via AutoMapper and returns it; the
   controller returns `201 Created`.

A read (e.g. `GET /api/claims/{id}`) is the same shape minus the validation-behavior/audit/commit
steps: controller → `GetClaimDetailQueryHandler` → repository read → DTO.

## Domain events

Two different mechanisms, deliberately — see ADR-0004 for the full reasoning:

- **Same-database effects** (e.g. a handler needing to react to something another handler just
  did, within the same request) use in-process MediatR `INotification` handlers, synchronously,
  before the transaction commits. Cheap, no extra moving parts, because everything involved is one
  `DbContext`/one transaction.
- **Cross-store effects** — specifically, reserve approval needing to eventually enqueue a job in
  Hangfire's own, separate SQL Server store — use the **outbox pattern**, because a local
  transaction can't span two different databases. `ApproveReserveCommandHandler` (and the
  auto-approval path in `OpenReserveCommandHandler`) writes the `ReserveHistory` row **and** an
  `OutboxMessage` row (`{ ReserveHistoryId, IdempotencyKey }`) in the same transaction, same
  database. A separate recurring job, `OutboxDispatcherJob` (polled every minute via
  `IRecurringJobManager`, see Program wiring in `InfrastructureServiceCollectionExtensions`), reads
  unprocessed outbox rows, calls `BackgroundJobClient.Enqueue<PostGLReserveChangeJob>`, and marks
  the row processed — guaranteeing "reserve approved" and "GL job will eventually run" are atomic
  from the caller's perspective without a distributed transaction.

## Hangfire job design

Hangfire is wired against its **own** SQL Server database (`ConnectionStrings:HangfireDatabase`),
separate from the main `ClaimsDatabase` — mirroring a more realistic production topology where a
job-processing store isn't the same database as the application's OLTP data.

| Job | Trigger | Idempotency |
|---|---|---|
| `OutboxDispatcherJob` | Recurring, every minute (`Cron.Minutely`) | Reads only unprocessed `OutboxMessage` rows and marks each processed in the same transaction as the enqueue; a crash between "enqueued" and "marked processed" can double-enqueue `PostGLReserveChangeJob`, which is itself idempotent (below) so a duplicate enqueue is harmless. |
| `PostGLReserveChangeJob` | Enqueued by the dispatcher above (never scheduled directly) | Defense in depth (ADR-0005): primary check against `ReserveHistory.PostingStatus` before writing (no-op if already `Posted`); backstop is a DB-level unique constraint on `IdempotencyKey` (`Reserve:{ReserveId}:Change:{ChangeSequence}`) plus a `RowVer`-conflict re-check that distinguishes "someone else already posted this" (benign, swallow) from a genuine concurrent-write conflict (rethrow, let Hangfire retry). `[AutomaticRetry(Attempts = 3, DelaysInSeconds = [5, 15, 30])]`; retry exhaustion is caught by `GLPostingFailureFilter`/`GLPostingFailureHandler`, which marks `PostingStatus = Failed` and writes a `GL_POSTING_FAILED` audit entry. |
| `SlaMonitoringJob` | Recurring, every 15 minutes (`*/15 * * * *`) | Only emits a new `SLA_BREACH_DETECTED` audit entry for a claim if 24+ hours have passed since the last one for that same claim (§12.2) — re-running the job inside that window is a no-op, not a duplicate alert. |

`Hangfire:StartServer = false` disables the worker that dequeues jobs (used by
`ClaimsModule.ApiIntegrationTests` so tests can enqueue and run jobs deterministically, without a
live worker racing test assertions or Respawn's per-test reset) while keeping job *registration*
and *enqueuing* fully live against the real store.

## Azure architecture

**Nothing is deployed.** This was a deliberate scope decision recorded in `wayfinder/map.md`
("Azure deployment topology and CI/CD pipeline design... explicitly deferred to a separate future
effort"), made so the backend implementation itself could proceed without re-litigating
infrastructure choices mid-slice. There is no Bicep/ARM/Terraform in this repo and no CI/CD
pipeline file.

The intended target, per §19's minimum resource list and this codebase's existing
provider-abstraction seams:

- **Azure App Service** for `ClaimsModule.API` — no code changes needed, just configuration
  (connection strings + `StorageProvider: AzureBlob` as App Service settings).
- **Azure SQL Database** × 2 (main + Hangfire store) — the codebase already assumes two separate
  connection strings; this maps directly.
- **Azure Blob Storage** — `AzureBlobStorageService` already exists and is already the default
  (`StorageProvider: AzureBlob`); only a real connection string is needed, nothing in the code.
- **Azure Static Web App** for the Angular frontend, once it exists.
- **Azure Key Vault** (optional per §19) for the JWT signing key and storage connection strings,
  instead of App Service app settings in plaintext — not implemented, noted as the natural
  upgrade.

## Key design decisions and tradeoffs

The full reasoning for every hard-to-reverse call lives in `docs/adr/0001`–`0009`
(`wayfinder/map.md` is the index). Summary:

| ADR | Decision | Why it's a real tradeoff |
|---|---|---|
| [0001](docs/adr/0001-claim-number-sequence.md) | One global, never-resetting SQL `SEQUENCE` for claim numbers | Simpler write path than a per-year counter table, at the cost of the 7-digit part not resetting each year (cosmetic, not correctness) |
| [0002](docs/adr/0002-reserve-balance-sequential-delta.md) | `CurrentAmount` is a persisted column updated via sequential delta, not a read-time `SUM()` | Matches the schema as specified, but means every reserve-mutating path must go through the one code path that keeps history and balance in lockstep — no self-healing reconciliation |
| [0003](docs/adr/0003-repository-unit-of-work.md) | Per-aggregate repositories + a named `IUnitOfWork`, repositories' `CommitChangesAsync()` delegates to it | Gives the Unit-of-Work pattern a visible, testable seam instead of an implicit shared `DbContext` |
| [0004](docs/adr/0004-domain-events-outbox-for-hangfire.md) | In-process MediatR notifications for same-DB effects; outbox pattern only to bridge into Hangfire's separate store | Avoids a distributed transaction (`TransactionScope`) across two SQL Server databases, at the cost of one more moving part (the dispatcher) that must itself be reliable |
| [0005](docs/adr/0005-gl-posting-idempotency.md) | Application check-then-insert as the primary idempotency path, DB unique constraint as the backstop | Constraint violations are an expected, swallowed outcome on this path, not an error — easy to get backwards if a future change adds a new GL-posting code path that doesn't know this |
| [0006](docs/adr/0006-exception-to-status-code-mapping.md) | `422` reserved for validation/business-rule failures only; not-found → `404`; concurrency → `409` | Every new domain exception type needs an explicit mapping entry or it silently falls through to `500` |
| [0007](docs/adr/0007-jwt-auth-self-hosted-test-issuer.md) | Real `AddJwtBearer` validation against a self-hosted, config-driven, dev/test-only token issuer | Swapping in a real IdP for production is a config change, not a code change — but nobody should mistake the dev issuer for production-appropriate auth |
| [0008](docs/adr/0008-validation-severity-and-business-rule-error-shape.md) | `POST /api/claims/validate` (filling a spec gap) + severity-aware `ValidationBehavior` + a second `blockingConditions` array in the `422` shape for non-field business-rule failures | Two exception types now feed one response shape (`ValidationException` → `errors`; `BusinessRuleViolationException` → `blockingConditions`) — in practice mutually exclusive per request, but the middleware's merge behavior for the theoretical both-fire case stays an explicit, documented choice rather than an accident |
| [0009](docs/adr/0009-testing-strategy.md) | Three test projects by cost tier; one shared SQL Server container (2 databases) + one Azurite container per assembly; Respawn, not transaction rollback, for isolation | Transaction-rollback-per-test would hide exactly the cross-process scenarios (outbox dispatch, Hangfire job execution) this suite exists to cover, since a rolled-back row is never visible to a separate process reading it |
