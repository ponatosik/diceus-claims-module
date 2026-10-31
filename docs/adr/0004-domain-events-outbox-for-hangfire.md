# In-process domain events, outbox only to bridge into Hangfire's separate store

Hangfire uses its own SQL Server database, separate from the main application database (see also ADR-0005's idempotency design, which assumes this split). That means a reserve-approval transaction (writing `ReserveHistory` in the main DB) and enqueueing the GL posting job (writing to Hangfire's store) can't commit atomically in one local transaction — there's no cheap distributed transaction here. Same-database effects (e.g., `ClaimCreated` triggering an audit log write) don't have this problem and are handled synchronously via in-process MediatR `INotification` handlers before the transaction commits.

For the one case that crosses stores — triggering the Hangfire GL posting job after a reserve is approved — we use the **outbox pattern**: approving a reserve writes the `ReserveHistory` row *and* an `OutboxMessage` row (payload: `ReserveHistoryId`, `IdempotencyKey`) in the same transaction, same database. A separate dispatcher polls unprocessed outbox rows, calls `BackgroundJob.Enqueue` against Hangfire's store, and marks the message processed. This guarantees "reserve approved" and "GL job will eventually be enqueued" are atomic from the caller's perspective, without needing a distributed transaction across the two stores.

## Considered Options

- **Distributed transaction (`TransactionScope`) across both databases**: would let Hangfire's SQL Server storage enlist directly, avoiding a bespoke outbox. Rejected — adds MSDTC/distributed-transaction infrastructure complexity disproportionate to a take-home, and Hangfire's SQL storage provider's distributed-transaction support is a sharper edge to debug live than a small outbox table.
- **Same database for both the app and Hangfire** (eliminating the cross-store problem entirely): would make the outbox unnecessary. Explicitly rejected per the "separate store for Hangfire" decision — kept the stores apart to mirror a more realistic production topology.

## Consequences

The outbox dispatcher is a new moving part (a recurring poll loop or hosted service) that must itself be reliable and must respect the same idempotency key when calling `BackgroundJob.Enqueue`, so a dispatcher crash-and-retry can't double-enqueue.
