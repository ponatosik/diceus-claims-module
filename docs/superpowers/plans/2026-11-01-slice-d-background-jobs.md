# Slice D: Background Jobs — Outbox Dispatcher, GL Posting, SLA Monitoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire Hangfire against its own separate SQL Server database, implement the outbox dispatcher that bridges Slice C's `OutboxMessage` rows into real Hangfire job enqueues, implement the idempotent GL posting job, and implement the recurring SLA monitoring job.

**Architecture:** `ClaimsModule.Infrastructure` gains Hangfire storage/server wiring and three job classes (`PostGLReserveChangeJob`, `OutboxDispatcherJob`, `SlaMonitoringJob`), all plain DI-resolved classes with an `ExecuteAsync` entry point so tests can invoke them directly without waiting on Hangfire's own scheduler. `Domain`/`Application` stay free of Hangfire types; jobs depend only on existing repository/audit interfaces plus two small additions (`IOutboxRepository`, and two new query methods on `IClaimRepository`/`IAuditLogService`).

**Tech Stack:** Hangfire.AspNetCore + Hangfire.SqlServer (new), existing MediatR/EF Core/FluentValidation stack unchanged.

**Spec:** `specs/004-background-jobs.md`; `REQUIREMENTS.md` §12 (Background Processing); ADR-0004 (outbox), ADR-0005 (GL posting idempotency), ADR-0009 (testing strategy, two-database topology).

## Global Constraints

- Job names (exact, per §12's table): `PostGLReserveChangeJob`, `SlaMonitoringJob`.
- GL posting idempotency key format (already baked into `ReserveHistory.IdempotencyKey` since Slice C): `Reserve:{ReserveId}:Change:{ChangeSequence}`.
- SLA job cron: `*/15 * * * *`. SLA threshold: `Status IN (Draft, Open)` and last-activity `< now - 48h`. SLA dedupe: suppress a new `SLA_BREACH_DETECTED` if one was already written for that claim within the last 24h.
- `GL_POSTING_SIMULATED` audit description: `"DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = {reserveAmount}."` (§6.5/§12.1, user story 2).
- `GL_POSTING_FAILED` is written only once Hangfire's automatic retries are exhausted (§12.1's failure-handling row); `ReserveHistory.PostingStatus` becomes `Failed` at that point.
- Hangfire storage: a second SQL Server database, connection string key `ConnectionStrings:HangfireDatabase`. `ClaimsModule.Infrastructure` owns all Hangfire types/registration; `Domain`/`Application` have zero dependency on them (enforced by the existing `ArchitectureTests.cs` fitness suite).
- Correlation id placeholder (wayfinder ticket 11, still open): each job execution gets a **fresh** trace id, not the originating HTTP request's — per the spec's own documented interim decision.

## Resolved Ambiguities

1. **GL job method signature omits `ClaimId`.** §12.1 lists the job's input as "ReserveHistoryId, ClaimId, IdempotencyKey", but `ReserveHistory.ClaimId` is already denormalised onto the row the job loads by `ReserveHistoryId` anyway (needed regardless, for the audit entry and the component lookup) — passing `ClaimId` as a second, independent argument would be redundant data the job never needs to trust independently. `ExecuteAsync(Guid reserveHistoryId, string idempotencyKey, string postingJobId)` is the actual signature; `ClaimId` is derived from the loaded `ReserveHistory` row.
2. **`PostingJobId` is a dispatcher-generated id, not Hangfire's own internal job id.** Retrieving Hangfire's own id from inside a job method requires a `PerformContext` parameter, whose special-parameter injection semantics (what Hangfire auto-populates vs. what must be passed explicitly) vary enough across integration patterns to be a needless risk for what this column actually needs: *some* value identifying which execution posted the entry. `OutboxDispatcherJob` generates a `Guid.NewGuid().ToString()` before enqueueing and passes it through as an explicit argument.
3. **ADR-0005's "unique-constraint backstop" is reinterpreted as `ReserveHistory.RowVer`'s optimistic-concurrency token.** ADR-0005 (written before Slice C's final review fix pass) describes the race backstop as a duplicate-key violation on the `IdempotencyKey` unique constraint. But the GL job's actual write is an **update** of the existing `ReserveHistory` row (not an insert with that key), and that row now carries a `RowVer` concurrency token added specifically to stop two concurrent writers from racing on the same row — exactly this scenario. Catching `DbUpdateConcurrencyException` around the commit achieves ADR-0005's exact stated goal (treat a race the pre-check missed as already-processed, terminate gracefully, never retry it as a failure) without adding a redundant marker table purely to get a literal unique-constraint violation. This plan documents the reinterpretation here rather than amending the ADR file itself.
4. **No Hangfire dashboard endpoint wired.** The spec's Out of Scope list defers "Hangfire dashboard authentication/exposure" as a deployment concern; the simplest reading consistent with that is to not call `app.UseHangfireDashboard()` at all in this slice (storage + server only) rather than wire an unauthenticated dashboard and immediately flag it as a problem.
5. **Final-failure detection (§12.1's "after all retries exhausted") uses a Hangfire global `IApplyStateFilter`, not in-job retry-count inspection.** The alternative (reading Hangfire's injected `PerformContext`/retry-count from inside the job method) ties the method's testability to exactly how Hangfire populates special parameters, which differs by how the method is invoked (directly in a test vs. through Hangfire's real pipeline). The filter is deliberately thin — it extracts `ReserveHistoryId` from the failed job's arguments and calls a small, directly-testable `GLPostingFailureHandler.RecordFinalFailureAsync` method, which carries all the actual business logic and gets real test coverage. The filter itself (Hangfire-specific glue, synchronous-over-async per `IApplyStateFilter`'s interface shape) is reviewed by eye, not unit-tested directly — consistent with how this codebase already treats `Program.cs`'s thin startup wiring.
6. **Outbox dispatcher does not need `AddHangfireServer()` actively running during `ApiIntegrationTests`.** The spec's own testing decision says to trigger jobs "synchronously inside a test rather than waiting on Hangfire's own scheduler." `ApiFixture` therefore keeps Hangfire storage + client registered (so `OutboxDispatcherJob.ExecuteAsync` can genuinely enqueue into real storage) but removes the actual `BackgroundJobServer` hosted service from the test host, so no job auto-processes in the background during a test run — preventing cross-test timing interference and avoiding a live server racing Respawn's reset of the same database. The real app (`Program.cs`) keeps the server registered for actual production processing.
7. **`ApiFixture.ResetAsync` gains a second Respawner for the Hangfire stand-in database**, per ADR-0009's explicit "both databases emptied before each test" — Hangfire's SQL storage auto-creates its schema at `AddHangfire(...)` registration time (before the host finishes building), so a second `Respawner.CreateAsync` against `HangfireConnectionString` is safe to construct lazily on first use, mirroring the existing main-database Respawner's lazy-init pattern.

## Review Focus

- Running `PostGLReserveChangeJob.ExecuteAsync` twice with the same `reserveHistoryId`/`idempotencyKey` must produce exactly one `GL_POSTING_SIMULATED` audit entry and leave `PostingStatus` at `Posted` — not two entries, and not an exception on the second call.
- Two near-simultaneous executions racing on the same `PendingApproval`→posting transition (simulated deterministically, not via timing) must result in exactly one successful posting; the loser's `DbUpdateConcurrencyException` must be swallowed internally, not surfaced as a Hangfire-retriable failure or a second audit entry.
- A claim that has never been modified since creation (`UpdatedAt` still `null`, only `CreatedAt` set) sitting in `Open` for 50+ hours must still be flagged by the SLA job — a literal `WHERE UpdatedAt < threshold` SQL translation would silently miss it, which a reasonable person relying on "flag stale claims" would not expect.
- The SLA job's 24-hour dedupe window must be exact on both sides: a breach recorded 23 hours ago suppresses a new one for that claim; one recorded 25 hours ago does not.
- Simulating a dispatcher crash-and-retry (the same unprocessed `OutboxMessage` row read and dispatched twice before either is marked processed) must not produce a user-visible duplicate — covered end-to-end by the GL job's own idempotency, but worth a dispatcher-level test proving the double-dispatch path is actually harmless, not just asserting the dispatcher's happy path once.

---

### Task 1: Hangfire storage wiring + two-database test topology

**Files:**
- Modify: `backend/src/ClaimsModule.Infrastructure/ClaimsModule.Infrastructure.csproj` (add `Hangfire.AspNetCore`, `Hangfire.SqlServer` via `dotnet add package`)
- Create: `backend/src/ClaimsModule.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- Modify: `backend/src/ClaimsModule.API/Program.cs`
- Modify: `backend/src/ClaimsModule.API/appsettings.json` (add `ConnectionStrings:HangfireDatabase: ""`)
- Modify: `backend/tests/ClaimsModule.ApiIntegrationTests/ApiFixture.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/BackgroundJobsWiringTests.cs` (new)

**Interfaces:**
- Produces: `InfrastructureServiceCollectionExtensions.AddInfrastructure(IServiceCollection, IConfiguration) : IServiceCollection` (registers `AddHangfire(...)` against `ConnectionStrings:HangfireDatabase`, `AddHangfireServer()`, and empty-for-now DI registrations for the job classes added in later tasks) and `InfrastructureServiceCollectionExtensions.UseInfrastructure(IServiceProvider)` (registers global job filters + recurring jobs — empty body for now, later tasks each add one line). Consumed by `Program.cs` and by every later task in this plan.
- Produces on `ApiFixture`: existing `HangfireConnectionString` property (already present, unused until now) gets wired into `ConfigureWebHost`'s in-memory config under `ConnectionStrings:HangfireDatabase`; `ResetAsync()` also resets the Hangfire stand-in database. Consumed by every later task's tests.

- [ ] **Step 1: Add the Hangfire packages**

```bash
cd backend
dotnet add src/ClaimsModule.Infrastructure package Hangfire.AspNetCore
dotnet add src/ClaimsModule.Infrastructure package Hangfire.SqlServer
```

- [ ] **Step 2: Write `InfrastructureServiceCollectionExtensions.AddInfrastructure`** — configures `AddHangfire(cfg => cfg.UseSqlServerStorage(configuration.GetConnectionString("HangfireDatabase")))` plus `AddHangfireServer()`, matching the existing `AddPersistence`/`AddApplication` extension-method convention (CLAUDE.md). `UseInfrastructure(IServiceProvider services)` can be an empty method body for now (later tasks add to it).

- [ ] **Step 3: Wire into `Program.cs`** — `builder.Services.AddInfrastructure(builder.Configuration);` alongside the existing `AddApplication()`/`AddPersistence()` calls; `app.Services.UseInfrastructure();` after `var app = builder.Build();`. Do not call `app.UseHangfireDashboard()` (Resolved Ambiguity 4).

- [ ] **Step 4: Wire `ApiFixture`** — add `[$"{"ConnectionStrings"}:HangfireDatabase"] = HangfireConnectionString` to the in-memory config in `ConfigureWebHost`. In `ConfigureWebHost`, also remove the `BackgroundJobServerHostedService` from the test host's service collection (via `builder.ConfigureServices(services => services.RemoveAll<IHostedService>(...))` or the equivalent precise removal — leave `AddHangfireServer()`'s other registrations, e.g. `JobStorage`/`IBackgroundJobClient`/`IRecurringJobManager`, intact) so no job auto-processes during tests (Resolved Ambiguity 6). Add a second lazily-constructed `Respawner` field (e.g. `_hangfireRespawner`/`_hangfireRespawnConnection`) against `HangfireConnectionString`, reset alongside the existing one in `ResetAsync()` (Resolved Ambiguity 7).

- [ ] **Step 5: Write the smoke test** — `BackgroundJobsWiringTests.The_app_starts_with_Hangfire_storage_wired_to_the_separate_database`: resolve `JobStorage.Current.GetMonitoringApi()` (or `IBackgroundJobClient` from the running app's `Services`) and assert a trivial call succeeds (e.g. `GetStatistics()` returns without throwing) — proving storage is reachable and pointed at the Hangfire stand-in database, not the main one.

- [ ] **Step 6: Run `dotnet build backend` and the new test** — `dotnet test backend/tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~BackgroundJobsWiringTests"` → expect PASS.

- [ ] **Step 7: Commit**

---

### Task 2: `IOutboxRepository`

**Files:**
- Create: `backend/src/ClaimsModule.Application/IOutboxRepository.cs`
- Create: `backend/src/ClaimsModule.Persistence/OutboxRepository.cs`
- Modify: `backend/src/ClaimsModule.Persistence/PersistenceServiceCollectionExtensions.cs` (register `IOutboxRepository, OutboxRepository`)
- Test: `backend/tests/ClaimsModule.PersistenceTests/OutboxRepositoryTests.cs` (new, or add to existing `OutboxMessageTests.cs` if that file already covers this entity — check first)

**Interfaces:**
- Consumes: `OutboxMessage` (existing, `ClaimsModule.Domain`), `ClaimsDbContext.OutboxMessages` (existing `DbSet`), `IUnitOfWork` (existing).
- Produces: `IOutboxRepository.ListUnprocessedAsync(CancellationToken = default) : Task<IReadOnlyList<OutboxMessage>>` (rows with `ProcessedAt == null`, ordered by `CreatedAt`) and `IOutboxRepository.CommitChangesAsync(CancellationToken = default) : Task`. Consumed by Task 4's `OutboxDispatcherJob`.

- [ ] **Step 1: Write the failing test** — insert two `OutboxMessage` rows (one with `ProcessedAt` already set, one without) directly via the fixture's `DbContext`; call `ListUnprocessedAsync`; assert it returns only the unprocessed one.
- [ ] **Step 2: Run it, verify it fails** (interface/class don't exist yet).
- [ ] **Step 3: Implement `IOutboxRepository`/`OutboxRepository`** exactly as described above; register in `PersistenceServiceCollectionExtensions.AddPersistence`.
- [ ] **Step 4: Run it, verify it passes.** `dotnet test backend/tests/ClaimsModule.PersistenceTests --filter "FullyQualifiedName~Outbox"`
- [ ] **Step 5: Commit**

---

### Task 3: `PostGLReserveChangeJob` + `GLPostingFailureHandler`

**Files:**
- Create: `backend/src/ClaimsModule.Infrastructure/BackgroundJobs/BackgroundJobActivitySource.cs`
- Create: `backend/src/ClaimsModule.Infrastructure/BackgroundJobs/PostGLReserveChangeJob.cs`
- Create: `backend/src/ClaimsModule.Infrastructure/BackgroundJobs/GLPostingFailureHandler.cs`
- Create: `backend/src/ClaimsModule.Infrastructure/BackgroundJobs/GLPostingFailureFilter.cs`
- Modify: `backend/src/ClaimsModule.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (register the three classes as scoped services in `AddInfrastructure`; register `GLPostingFailureFilter` into `GlobalJobFilters.Filters` inside `UseInfrastructure`, guarded against duplicate registration)
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/GLPostingJobTests.cs` (new — the dedicated class ADR-0009 reserved space for)

**Interfaces:**
- Consumes: `IClaimReserveComponentRepository` (existing: `GetHistoryByIdAsync`, `GetByIdAsync`, `CommitChangesAsync`), `IAuditLogService.LogAsync` (existing), `AuditLogEntry` (existing), `ReservePostingStatus` (existing enum: `Pending/Posted/Failed/Cancelled`).
- Produces: `BackgroundJobActivitySource.Source : ActivitySource` (name `"ClaimsModule.BackgroundJobs"`), consumed by Tasks 4 and 5's jobs too, for the same fresh-correlation-id-per-execution placeholder (Global Constraints). `PostGLReserveChangeJob.ExecuteAsync(Guid reserveHistoryId, string idempotencyKey, string postingJobId) : Task` — the dispatcher's enqueue target (Task 4). `GLPostingFailureHandler.RecordFinalFailureAsync(Guid reserveHistoryId, string failureReason, CancellationToken = default) : Task` — called by `GLPostingFailureFilter` and directly by tests.

- [ ] **Step 1: Write the failing tests** in `GLPostingJobTests`:
  - `Posting_a_pending_reserve_sets_Posted_and_writes_the_GL_POSTING_SIMULATED_audit_entry`: approve a reserve through the real API (reuse Slice C's `ApproveReserveTests` helper pattern), resolve `PostGLReserveChangeJob` from `Fixture.Services`, call `ExecuteAsync(historyId, idempotencyKey, "job-1")` directly, then assert `PostingStatus == Posted` and exactly one `GL_POSTING_SIMULATED` entry exists for the claim with the amount in its description.
  - `Running_the_job_twice_with_the_same_idempotency_key_produces_exactly_one_audit_entry`: call `ExecuteAsync` twice with identical arguments; assert the audit log still has exactly one `GL_POSTING_SIMULATED` entry.
  - `A_losing_concurrent_execution_is_swallowed_not_retried`: load the same `ReserveHistory` row via two separately-resolved `PostGLReserveChangeJob` instances/repositories (two DI scopes) so each holds a stale `RowVer`; run the first to completion (succeeds), then run the second — assert it does **not** throw and the audit log still has exactly one `GL_POSTING_SIMULATED` entry (Review Focus item 2; mirrors Slice C's deterministic `ReserveHistory_RowVer_rejects_a_stale_concurrent_update` PersistenceTests pattern, but exercised through the job's own exception handling instead of a raw `SaveChangesAsync` call).
  - `GLPostingFailureHandler_RecordFinalFailureAsync_sets_Failed_and_writes_GL_POSTING_FAILED`: call the handler directly against a `Pending` reserve; assert `PostingStatus == Failed` and a `GL_POSTING_FAILED` entry exists describing the failure reason.
- [ ] **Step 2: Run, verify fail** (classes don't exist).
- [ ] **Step 3: Implement** `BackgroundJobActivitySource`, `PostGLReserveChangeJob.ExecuteAsync` (load history → no-op if already `Posted` → load component → set `PostingStatus = Posted`/`PostingJobId = postingJobId` → start an activity from `BackgroundJobActivitySource.Source` → write the `GL_POSTING_SIMULATED` audit entry (description per Global Constraints, using `history.Amount`) → `CommitChangesAsync()` wrapped in `try`/`catch (DbUpdateConcurrencyException)` that swallows per Resolved Ambiguity 3), `GLPostingFailureHandler.RecordFinalFailureAsync` (load history → no-op if `null` or already `Posted` → set `Failed` → audit `GL_POSTING_FAILED` → commit), `[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 5, 15, 30 })]` on `PostGLReserveChangeJob`, and `GLPostingFailureFilter : JobFilterAttribute, IApplyStateFilter` (constructor takes `IServiceProvider`; `OnStateApplied` checks `context.NewState is FailedState && context.BackgroundJob.Job.Type == typeof(PostGLReserveChangeJob)`, extracts `reserveHistoryId` from `context.BackgroundJob.Job.Args[0]`, creates a scope, resolves `GLPostingFailureHandler`, calls `RecordFinalFailureAsync(...).GetAwaiter().GetResult()` — `IApplyStateFilter` has no async overload, this blocking call is Hangfire's own API shape, not a code-quality lapse; document with a one-line comment). `OnStateUnapplied` is a no-op.
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~GLPostingJobTests"`
- [ ] **Step 5: Commit**

---

### Task 4: `OutboxDispatcherJob`

**Files:**
- Create: `backend/src/ClaimsModule.Infrastructure/BackgroundJobs/EnqueueGLPostingJobPayload.cs` (record matching Slice C's `OpenReserveCommandHandler`/`ApproveReserveCommandHandler` outbox payload shape: `Guid ReserveHistoryId`, `string IdempotencyKey`)
- Create: `backend/src/ClaimsModule.Infrastructure/BackgroundJobs/OutboxDispatcherJob.cs`
- Modify: `backend/src/ClaimsModule.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (register `OutboxDispatcherJob` as scoped; add `RecurringJob.AddOrUpdate<OutboxDispatcherJob>("outbox-dispatcher", j => j.ExecuteAsync(), Cron.Minutely)` to `UseInfrastructure`)
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/OutboxDispatcherJobTests.cs` (new)

**Interfaces:**
- Consumes: `IOutboxRepository` (Task 2), `IBackgroundJobClient.Enqueue<PostGLReserveChangeJob>(Expression<Func<PostGLReserveChangeJob, Task>>) : string` (Hangfire), `PostGLReserveChangeJob.ExecuteAsync` (Task 3's exact signature).
- Produces: `OutboxDispatcherJob.ExecuteAsync(CancellationToken = default) : Task`. Not consumed elsewhere in this plan (registered directly as the recurring job's target) — tests call it directly.

- [ ] **Step 1: Write the failing tests** in `OutboxDispatcherJobTests`:
  - `Dispatching_an_unprocessed_outbox_row_enqueues_the_GL_job_and_marks_it_processed`: approve a reserve through the real API (producing a real `OutboxMessage` row per Slice C), resolve `OutboxDispatcherJob` and call `ExecuteAsync()`; assert the outbox row's `ProcessedAt` is now non-null, and that `JobStorage.Current.GetMonitoringApi()` shows one enqueued job (or assert on `IBackgroundJobClient.Enqueue`'s returned job id being non-empty, captured via a `Mock<IBackgroundJobClient>`/real client — prefer the real client against real storage per ADR-0009's intent, asserting via the monitoring API).
  - `Dispatching_the_same_row_twice_before_either_is_marked_processed_is_still_harmless` (Review Focus item 5): simulate the crash-and-retry case directly — call `ExecuteAsync()` twice without anything marking the row processed in between (e.g. by re-querying the same still-unprocessed row and dispatching it again before the first dispatch's "mark processed" commit, or simplest: call the job's internal dispatch-one-message logic twice against the same loaded row) — then separately resolve `PostGLReserveChangeJob` and run it for both enqueues' arguments; assert still exactly one `GL_POSTING_SIMULATED` audit entry (the GL job's own idempotency, Task 3, absorbs the duplicate).
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement** `EnqueueGLPostingJobPayload`, `OutboxDispatcherJob.ExecuteAsync` (list unprocessed messages → for each, `switch` on `Type` with a case for `"EnqueueGLPostingJob"` that deserializes `Payload` into `EnqueueGLPostingJobPayload`, generates `var postingJobId = Guid.NewGuid().ToString();`, calls `backgroundJobClient.Enqueue<PostGLReserveChangeJob>(j => j.ExecuteAsync(payload.ReserveHistoryId, payload.IdempotencyKey, postingJobId))`, sets `message.ProcessedAt = DateTimeOffset.UtcNow` → `default` case: leave unprocessed, nothing to do yet → `CommitChangesAsync()` once at the end). Add the recurring-job registration line to `UseInfrastructure`.
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~OutboxDispatcherJobTests"`
- [ ] **Step 5: Commit**

---

### Task 5: `SlaMonitoringJob`

**Files:**
- Modify: `backend/src/ClaimsModule.Application/IClaimRepository.cs` (add `ListStaleAsync`)
- Modify: `backend/src/ClaimsModule.Persistence/ClaimRepository.cs` (implement it)
- Modify: `backend/src/ClaimsModule.Application/Common/IAuditLogService.cs` (add `GetLastEventTimestampAsync`)
- Modify: `backend/src/ClaimsModule.Persistence/AuditLogService.cs` (implement it)
- Create: `backend/src/ClaimsModule.Infrastructure/BackgroundJobs/SlaMonitoringJob.cs`
- Modify: `backend/src/ClaimsModule.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (register `SlaMonitoringJob` as scoped; add `RecurringJob.AddOrUpdate<SlaMonitoringJob>("sla-monitoring", j => j.ExecuteAsync(), "*/15 * * * *")` to `UseInfrastructure`)
- Test: `backend/tests/ClaimsModule.PersistenceTests/ClaimRepositoryTests.cs` or existing equivalent (for `ListStaleAsync`), `backend/tests/ClaimsModule.PersistenceTests/AuditLogServiceTests.cs` or equivalent (for `GetLastEventTimestampAsync`) — check for existing files covering these classes before creating new ones
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/SlaMonitoringJobTests.cs` (new)

**Interfaces:**
- Consumes: `IClaimRepository.GetByIdAsync`/`CommitChangesAsync` (existing), `IAuditLogService.LogAsync` (existing), `BackgroundJobActivitySource.Source` (Task 3).
- Produces: `IClaimRepository.ListStaleAsync(IReadOnlyCollection<ClaimStatus> statuses, DateTimeOffset updatedBefore, CancellationToken = default) : Task<IReadOnlyList<Claim>>` — claims where `statuses.Contains(c.Status)` and `(c.UpdatedAt ?? c.CreatedAt) < updatedBefore` (Review Focus item 3's `UpdatedAt`-fallback). `IAuditLogService.GetLastEventTimestampAsync(Guid claimId, string eventType, CancellationToken = default) : Task<DateTimeOffset?>` — most recent `CreatedAt` among `ClaimAuditLog` rows for that claim/event type, or `null` if none exist. `SlaMonitoringJob.ExecuteAsync(CancellationToken = default) : Task`.

- [ ] **Step 1: Write the failing repository/service tests**:
  - `ListStaleAsync` test: seed one `Open` claim with `UpdatedAt` null and `CreatedAt` 50 hours ago, one `Open` claim updated 1 hour ago, one `Closed` claim created 100 hours ago; assert only the first is returned (proves both the status filter and the `UpdatedAt ?? CreatedAt` fallback).
  - `GetLastEventTimestampAsync` test: seed two `ClaimAuditLog` rows of different event types at different times for the same claim; assert it returns the most recent `CreatedAt` for the matching event type only, and `null` for a claim/event-type combination with no rows.
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement both.**
- [ ] **Step 4: Run, verify pass.**
- [ ] **Step 5: Write the failing job tests** in `SlaMonitoringJobTests`:
  - `A_stale_Open_claim_gets_a_SLA_BREACH_DETECTED_entry`: create a claim via the real API, backdate its `CreatedAt`/leave `UpdatedAt` null via raw SQL to 50 hours ago, resolve `SlaMonitoringJob`, call `ExecuteAsync()`, assert a `SLA_BREACH_DETECTED` entry now exists with the exact description `"Claim has not been updated in 48 hours."`.
  - `Running_it_again_within_24_hours_does_not_duplicate_the_breach`: run `ExecuteAsync()` again immediately after the previous test's run; assert still exactly one `SLA_BREACH_DETECTED` entry for that claim.
  - `A_breach_more_than_24_hours_old_allows_a_new_one` (Review Focus item 4's boundary): backdate the existing `SLA_BREACH_DETECTED` entry's `CreatedAt` to 25 hours ago via raw SQL, run `ExecuteAsync()` again, assert a second entry now exists; separately (or via `InlineData`), backdating to 23 hours ago must NOT produce a second entry.
  - `A_Closed_claim_is_never_flagged_even_if_stale`: a `Closed` claim backdated the same way produces no `SLA_BREACH_DETECTED` entry.
- [ ] **Step 6: Run, verify fail, implement `SlaMonitoringJob.ExecuteAsync`** (list stale claims via `ListStaleAsync([Draft, Open], UtcNow.AddHours(-48))` → for each, check `GetLastEventTimestampAsync(claim.ClaimId, "SLA_BREACH_DETECTED")`; if `null` or older than `UtcNow.AddHours(-24)`, start an activity from `BackgroundJobActivitySource.Source` and write the audit entry → `CommitChangesAsync()` once at the end). Add the recurring-job registration line to `UseInfrastructure`.
- [ ] **Step 7: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~SlaMonitoringJobTests"`
- [ ] **Step 8: Commit**

---

## Self-Review

**Spec coverage:** All 10 user stories in `specs/004-background-jobs.md` map to a task — 1/9 → Task 1 (wiring) + Task 4 (dispatcher); 2/3/4/5 → Task 3; 6 → Task 3's `GLPostingFailureHandler`; 7/8 → Task 5; 10 → the dedicated `GLPostingJobTests`/`OutboxDispatcherJobTests`/`SlaMonitoringJobTests` classes across Tasks 3–5, all in `ApiIntegrationTests` against the real two-database topology per ADR-0009.

**Proportion check:** five tasks, each with a handful of named tests and signatures, no full method bodies for anything whose shape isn't already pinned down by an existing pattern (repository methods mirror Slice C's exactly; job classes are the only genuinely new shape, and their bodies are described as an ordered list of steps rather than written out in full).

## Final steps (not a plan task — part of the `/implement` workflow wrapping this plan)

- [ ] Run `dotnet build` and `dotnet test backend/tests/ClaimsModule.UnitTests` after every task (fast loop).
- [ ] Run the full `dotnet test` (Docker required) once after Task 5.
- [ ] Dispatch a fresh whole-branch code review (most capable model), same process as Slices B and C's final reviews.
- [ ] Commit (already happening per-task above) — no separate final commit needed unless the review produces fixups.
