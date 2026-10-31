---
title: "Slice D: Background Jobs — Outbox Dispatcher, GL Posting, SLA Monitoring"
labels: ["ready-for-agent"]
status: open
---

## Problem Statement

Reserve approval (Slice C) writes an `OutboxMessage` row recording that a GL posting job needs to eventually run, but nothing reads that row or ever enqueues the job — the simulated general-ledger posting §6.5 describes never actually happens. Separately, there's no mechanism watching for claims that have gone quiet in `Draft`/`Open` status past the 48-hour SLA window (§12.2).

## Solution

Wire up Hangfire against its own separate SQL Server database (ADR-0004), implement the outbox dispatcher that bridges Slice C's `OutboxMessage` rows into Hangfire job enqueues, implement the GL posting job itself with its idempotency guarantee (ADR-0005), and implement the recurring SLA monitoring job (§12.2).

## User Stories

1. As a claims supervisor who just approved a reserve, I want a background process to eventually notice the `OutboxMessage` row my approval created and enqueue the GL posting job, so that the simulated ledger entry actually gets written without my approval request having to wait on it synchronously.
2. As a DICEUS reviewer, I want the GL posting job, once it runs, to write a `ClaimAuditLog` entry with `EventType = GL_POSTING_SIMULATED` describing the simulated journal (DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, with the reserve amount), so that the simulated financial event is part of the permanent audit trail (§6.5).
3. As a DICEUS reviewer, I want the GL posting job to update `ReserveHistory.PostingStatus` to `Posted` on success, so that the reserve detail UI can show which transactions have actually been "posted" versus are still pending.
4. As the system, I want the GL posting job to use the idempotency key `Reserve:{ReserveId}:Change:{ChangeSequence}` (§6.5), checking `ReserveHistory.PostingStatus`/`IdempotencyKey` before writing anything, with a database-level unique constraint as a defensive backstop (ADR-0005), so that the job is safe to run twice without producing duplicate audit entries.
5. As the system, when the unique-constraint backstop catches a race the pre-check missed, I want the job to treat it as "already processed" and terminate gracefully rather than retry forever against a constraint that can never succeed (ADR-0005), so that a rare race condition doesn't become an infinite retry loop.
6. As the system, if the GL posting job exhausts Hangfire's normal retry policy after genuine failures, I want `ReserveHistory.PostingStatus` set to `Failed` and a `GL_POSTING_FAILED` audit entry written describing the failure (§12.1), so that a stuck posting is visible and the frontend's "retry" button (§11.3) has something to act on.
7. As a claims handler, I want a recurring job to run every 15 minutes, find every claim in `Draft` or `Open` status that hasn't been updated in more than 48 hours, and write a `SLA_BREACH_DETECTED` audit entry for each one (§12.2), so that stale claims are flagged without anyone having to go looking for them.
8. As the system, I want the SLA monitoring job to never write a duplicate `SLA_BREACH_DETECTED` entry for the same claim within 24 hours of the last one (§12.2's idempotency rule), so that a claim stuck in `Open` for a week doesn't generate 672 identical audit rows.
9. As a DICEUS reviewer inspecting the deployed solution, I want Hangfire's dashboard/storage to live in a database genuinely separate from the main `ClaimsModule` database (ADR-0004), so that the architecture visibly matches what ADR-0004 committed to, not just in test infrastructure.
10. As the `ClaimsModule.ApiIntegrationTests` project, I want to exercise the full chain — approve a reserve, wait for (or directly trigger) the outbox dispatcher, confirm the GL job ran, confirm the audit entries and `PostingStatus` are correct — against the real two-database Testcontainers topology ADR-0009 already set up for exactly this, so that the hardest correctness area in the whole backend has real coverage, not mocks.

## Implementation Decisions

- **Hangfire storage**: a second SQL Server database, separate from the main `ClaimsModule` database, configured via its own connection string (`appsettings.json` key to be named during implementation, e.g. `ConnectionStrings:HangfireDatabase`). `ClaimsModule.Infrastructure` owns the Hangfire job definitions and startup registration (`AddHangfire`, `AddHangfireServer`, `RecurringJob.AddOrUpdate` for the SLA job) — `ClaimsModule.Domain`/`Application` have zero dependency on Hangfire types, matching the existing architecture-fitness test.
- **Outbox dispatcher**: a component (polling loop or a Hangfire-scheduled job itself) that reads unprocessed `OutboxMessage` rows from the main database, calls `BackgroundJob.Enqueue` against Hangfire's separate store for each, and marks the row processed (`ProcessedAt` set) only after a successful enqueue — using the same idempotency key so a dispatcher crash-and-retry can't double-enqueue (per ADR-0004's consequence).
- **GL posting job (`PostGLReserveChangeJob`)**: input is `ReserveHistoryId`, `ClaimId`, `IdempotencyKey`. Checks `ReserveHistory.PostingStatus` first (no-op if already `Posted`); relies on the DB unique constraint on `IdempotencyKey` as the race backstop. Writes the `GL_POSTING_SIMULATED` audit entry and updates `PostingStatus`/`PostingJobId` through `IAuditLogService`/the reserve repository — not a direct, ad-hoc `DbContext` write, consistent with AUD-02.
- **SLA monitoring job (`SlaMonitoringJob`)**: Hangfire recurring job, cron `*/15 * * * *`. Query: claims where `Status IN (Draft, Open)` and `UpdatedAt < now - 48h`. Idempotency: track the last `SLA_BREACH_DETECTED` entry's timestamp per claim (queryable from `ClaimAuditLog` itself — no separate tracking table needed) and skip claims where one exists within the last 24 hours. Does not change claim status — audit-only, per §12.2's explicit note.
- **Correlation**: wayfinder ticket 11 (still open) will determine exactly how `CorrelationId` is populated for these background-originated audit entries, since they execute outside any HTTP request context. Until that ticket resolves, this slice uses a freshly-generated correlation id per job execution as a placeholder, clearly marked so it's easy to find and update once ticket 11 lands.

## Testing Decisions

- **Seam: `ClaimsModule.ApiIntegrationTests`**, specifically the dedicated test class ADR-0009 already reserved space for ("GL posting idempotency gets a dedicated test class inside `ApiIntegrationTests`... given it's one of the trickiest pieces of correctness"). This is the one place in the backend that genuinely needs both databases (`fixture.HangfireConnectionString` alongside the main one) and is exactly what `ApiFixture` was built for.
- Good tests here specifically target the idempotency guarantee: running the GL posting job logic twice with the same idempotency key must produce exactly one audit entry and one `Posted` status, not two. Simulating the unique-constraint race (two near-simultaneous executions) is worth a dedicated test even if it requires forcing concurrency explicitly rather than relying on timing.
- The SLA monitoring job's 24-hour dedupe window is also worth a boundary test (a breach recorded 23 hours ago suppresses a new one; one recorded 25 hours ago does not).
- Prior art: none yet for background-job testing specifically — this slice establishes the pattern (how to trigger a Hangfire job synchronously inside a test rather than waiting on Hangfire's own scheduler) that would be referenced by any future background-job work.

## Out of Scope

- Wayfinder ticket 11's full resolution (correlation-id propagation for background jobs) — this slice uses a documented placeholder per the Implementation Decisions above, to be revisited once that ticket closes.
- Document upload/download — Slice E.
- Any notification/email integration — §12.2 explicitly notes the SLA job "does not send emails or push notifications; it only writes audit log entries."
- Hangfire dashboard authentication/exposure in the deployed environment — a deployment concern, out of scope for the backend decision log's destination.

## Further Notes

This slice is the direct payoff of ADR-0004's design — if the outbox pattern and the two-database topology don't actually work end-to-end here, that ADR's core bet was wrong and worth revisiting before building anything further on top of it.
