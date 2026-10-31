---
id: 2
title: "Reserve balance (CurrentAmount) persistence strategy"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

`ClaimReserveComponents.CurrentAmount` is specified as a persisted `DECIMAL(19,4)` column (§9.5) but described as "computed from the history table" (§9.6), and reserves use event sourcing (no direct UPDATE of the balance). Is `CurrentAmount` a persisted, write-time-maintained column, or a virtual value recomputed via `SUM()` at read time — and exactly when does it move, given `PendingApproval`/`Rejected` transactions must not count toward it (§9.5)?

## Resolution

`CurrentAmount` stays persisted (the schema already commits to it). It's maintained via **sequential delta tracking** using `ReserveHistory`'s existing `PreviousBalance`/`NewBalance` columns, not a full `SUM()` recomputation: each transaction reads the component's current `CurrentAmount` as `PreviousBalance`, computes `NewBalance = PreviousBalance + Amount`, and writes both — then writes `NewBalance` back into `ClaimReserveComponents.CurrentAmount` — all in the same transaction. Critically, this only happens when a transaction reaches **final-approved** status: immediately for auto-approved reserves, or at approval time (not creation time) for reserves that sat in `PendingApproval`. Full reasoning in [ADR-0002](../../docs/adr/0002-reserve-balance-sequential-delta.md).
