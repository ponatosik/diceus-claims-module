# BR-R-01's "amount > 0" governs opening a reserve; later changes are signed deltas floored by §6.2

BR-R-01 / §8 say a reserve transaction amount "must be greater than zero (except SubrogationRecoverable)". Read literally for every transaction, no reserve could ever be decreased or reversed — yet §6.1 says reserves are "adjusted throughout the claim lifecycle", §9.6 defines `ReserveHistory.Amount` as a signed delta ("positive = increase, negative = decrease") with `Add / Adjust / Reverse` transaction types, §11.3 colours decreases red, and §6.2's component table has a "May Go Negative?" column (No for Indemnity/Expense/ALAE, Yes for SubrogationRecoverable) that only means something if transactions can lower a balance. We read the spec as a whole: **BR-R-01's positivity rule applies to the amount that opens a component (`Add`)**; an `Adjust` is any non-zero signed delta; a `Reverse` must exactly cancel the current balance; and no transaction may take a non-SubrogationRecoverable **balance** below zero (§6.2). The balance rules are checked on submission and again on approval, since a pending transaction's effect depends on the balance at the moment it is applied.

## Considered Options

- **Literal BR-R-01 (increases only).** Rejected: makes `Adjust`/`Reverse`, signed deltas, red decreases and §6.2's "May Go Negative?" column meaningless, and leaves an over-reserved claim with no way down except closing it with open reserves (CC-04).

## Consequences

Whether a transaction *opens* a component is decided by the server from whether the component exists, not from the client's transaction type — so an `Adjust`/`Reverse` of a component that doesn't exist is refused (`RESERVE-TYPE-01`) and BR-R-01 can't be bypassed by labelling a negative opening "Adjust". Repeated `Add`s on an existing component still accumulate (the original Slice C contract), and the validator still requires an `Add` to be positive.

Authority tiers still apply to `|amount|` (`ReserveAuthority.GetTier`), so a large decrease needs the same approval as a large increase. New 422 codes: `RESERVE-TYPE-01`, `RESERVE-BALANCE-NEGATIVE-01`, `RESERVE-REVERSE-01` (constants on `ReserveBalanceRules`).
