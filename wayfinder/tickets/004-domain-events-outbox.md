---
id: 4
title: "Domain event dispatch mechanism, and Hangfire's data store"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

How are domain events (e.g. `ClaimCreated` → audit log entry, reserve approval → GL posting job enqueue) dispatched — synchronous in-process MediatR notifications, or an outbox pattern? And does Hangfire share the application's SQL Server database or use a separate store (which changes whether its enqueue calls can share a local transaction with the rest of a request)?

## Resolution

Hangfire uses **its own, separate SQL Server database** from the main application database — deliberately, to mirror a more realistic production topology. That means reserve-approval (main DB) and Hangfire job enqueue (separate store) can't commit atomically in one local transaction. Resolution: same-database domain events (e.g. `ClaimCreated` → audit log write) are handled **synchronously, in-process, via MediatR `INotification` handlers**, before the transaction commits. The one cross-store case — triggering the GL posting job after reserve approval — uses the **outbox pattern**: the approval transaction writes `ReserveHistory` *and* an `OutboxMessage` row (payload: `ReserveHistoryId`, `IdempotencyKey`) atomically in the main DB; a separate dispatcher polls unprocessed outbox rows and calls `BackgroundJob.Enqueue` against Hangfire's store, then marks the message processed. Full reasoning and rejected alternatives (distributed transaction, shared DB) in [ADR-0004](../../docs/adr/0004-domain-events-outbox-for-hangfire.md).

Note: this surfaced a new open question — see ticket "CorrelationId for background/outbox-dispatched audit entries" (frontier).
