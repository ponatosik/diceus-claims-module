# Reserve balances are persisted via sequential delta tracking, not recomputed by SUM

`ClaimReserveComponents.CurrentAmount` is specified as a persisted `DECIMAL(19,4)` column (§9.5) whose value is described as "computed from the history table" (§9.6). Rather than treating it as a virtual/computed value re-aggregated via `SUM()` over `ReserveHistory` on every read, we use the `PreviousBalance`/`NewBalance` columns `ReserveHistory` already carries: each transaction reads the component's current `CurrentAmount` as its `PreviousBalance`, computes `NewBalance = PreviousBalance + Amount`, writes both into the new `ReserveHistory` row, and writes `NewBalance` back into `ClaimReserveComponents.CurrentAmount` — all in the same transaction as the write that caused it.

Critically, `CurrentAmount` only moves when a transaction reaches a **final-approved** state: immediately for auto-approved reserves (≤$10,000), or at approval time (not creation time) for reserves that sat in `PendingApproval`. A `PendingApproval` or `Rejected` transaction is recorded in `ReserveHistory` but must not move `CurrentAmount`, per §9.5's "sum of all **posted/approved** history transactions."

## Considered Options

- **Pure `SUM()` aggregation at read time, no persisted column**: simpler event-sourcing purity, no write-side update to get wrong, but turns every claim-list/claim-detail read into an aggregate query per component and contradicts the schema as specified (§9.5 lists `CurrentAmount` as a real column). Rejected because the schema already commits to persisting it.

## Consequences

The persisted value can drift from the true sum if any code path writes a `ReserveHistory` row without also updating `CurrentAmount` in the same transaction. All reserve-mutating handlers must go through the one code path that keeps these in lockstep — there's no self-healing reconciliation.
