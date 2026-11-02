# Claims Module (FNOL & Reserve Management)

The vertical slice of an insurance claims system covering first-notice-of-loss intake, the claim status lifecycle, and reserve management with authority-gated approval.

## Language

### Reserves

**Authority Tier**:
The approval level a reserve transaction requires, determined solely by the absolute value of its amount: Auto (≤ $10,000), Supervisor (≤ $100,000), Manager (≤ $10,000,000). It does not depend on who submits it.
_Avoid_: approval level, threshold band

**Submitter**:
The user who created a reserve transaction; the only user who may retract it, and the one user who may never approve it.
_Avoid_: creator, requester, owner

**Pending Amount**:
The sum of a reserve component's transactions still in PendingApproval — not yet reflected in its current balance.
_Avoid_: unapproved balance, provisional amount

**Aggregate Limit Override**:
A Manager-granted, per-claim permission allowing total approved reserves to exceed $10,000,000.
_Avoid_: override flag, limit waiver

### Claims

**Pre-flight Check**:
The evaluation of every Blocking Condition and warning for a proposed status transition, done before the transition is attempted.
_Avoid_: dry run, closure check

**In-force**:
A policy is in-force for a claim when the claim's loss date falls within the policy's effective period.
_Avoid_: active policy, valid policy

**Blocking Condition**:
A named business rule (identified by its rule code, e.g. `CC-01`, `BR-R-03`) that currently prevents an action on the whole claim or reserve, as opposed to a problem with one submitted field.
_Avoid_: business error, general error

**Demo User**:
One of the three fixed simulated users (handler, supervisor, manager) the application can act as; stands in for real identities.
_Avoid_: test user, fake user
