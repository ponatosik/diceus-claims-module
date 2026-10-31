---
title: "Slice C: Reserve Management"
labels: ["ready-for-agent"]
status: open
---

## Problem Statement

A claim can be created and moved through most of its lifecycle, but there's no way to set, adjust, or approve the financial reserves that drive the claim's liability — and without a reserve ever reaching `Approved`, a claim can never legitimately reach `PendingPayment` or satisfy the `Closed` transition's conditions (CC-01).

## Solution

Implement reserve component management — opening and adjusting reserves, the three-tier authority workflow, approval/rejection/retraction, and self-approval prevention — on top of Slice A's schema (`ClaimReserveComponent`, `ReserveHistory`, `OutboxMessage`) and Slice B's shared plumbing (validation pipeline, audit logging, auth). Reserve approval is also where the outbox message gets written (ADR-0004) that Slice D's dispatcher will later pick up to enqueue the GL posting job — this slice writes that outbox row but does not implement anything that reads it.

## User Stories

1. As a claims handler, I want `POST /api/claims/{id}/reserves` to open a reserve component (`Indemnity`, `Expense`, `ALAE`, or `SubrogationRecoverable`) with an amount, component type, and change reason, so that I can record the claim's expected cost.
2. As a claims handler, I want a reserve amount ≤ $10,000 auto-approved immediately, regardless of my role (§6.3), so that routine, low-value reserves don't need a supervisor's time.
3. As a claims handler, I want a reserve amount > $10,000 and ≤ $100,000 to require Supervisor or Manager approval before it takes effect, and > $100,000 to require Manager approval, so that larger financial commitments get appropriate oversight (BR-R-02).
4. As a claims handler, I want reserve amounts required to be greater than zero, except `SubrogationRecoverable` which may be negative (BR-R-01), so that the one component representing a cost reduction is modeled correctly.
5. As a claims supervisor, I want `GET /api/claims/{id}/reserves` to show the current balance per component plus full transaction history, so that I can see what's pending my approval and what's already settled.
6. As a claims supervisor, I want `POST /api/claims/{id}/reserves/{txnId}/approve` to require my Supervisor or Manager role and reject the request if I'm the one who submitted it (BR-R-03, self-approval), so that no one can approve their own financial decision.
7. As a claims supervisor, I want approving a reserve to immediately update `ClaimReserveComponents.CurrentAmount` via sequential-delta tracking (ADR-0002: `NewBalance = PreviousBalance + Amount`, written into both `ReserveHistory` and the component) and to write an `OutboxMessage` for the eventual GL posting job, both in the same transaction as the approval, so that the balance update and the "a GL job must be enqueued" fact are atomic.
8. As a claims supervisor, I want `POST /api/claims/{id}/reserves/{txnId}/reject` to require Supervisor/Manager role, record a rejection reason, and leave the original transaction in history with status `Rejected` — not delete it (BR-R-04), so that the audit trail of what was proposed and why it was turned down is preserved.
9. As a claims handler whose reserve was rejected, I want to submit a new, revised reserve transaction rather than edit the rejected one, so that history stays append-only.
10. As a claims handler, I want `POST /api/claims/{id}/reserves/{txnId}/retract` to let me cancel my own pending reserve before anyone approves or rejects it, setting its status to `Cancelled`, so that I can correct a mistake without waiting on a supervisor.
11. As a claims handler, I want a `PendingApproval` reserve to be immutable — the only ways out are retract-then-resubmit, approve, or reject — so that what a supervisor is reviewing can't silently change underneath them.
12. As a claims manager, I want the system to warn when a new reserve would push a claim's total approved reserves (across all components) over $10,000,000, and require me specifically to set an override flag on the claim before that reserve can be approved (BR-R-05), so that catastrophic-value claims get explicit manager sign-off.
13. As a claims handler, I want reserve creation blocked with a Warning-turned-block when no policy is linked to the claim (BR-C-06's consequence: "reserve creation is blocked until a policy is linked"), so that financial actions never happen against an unidentified policy.
14. As a DICEUS reviewer, I want every reserve lifecycle event (`RESERVE_CREATED`, `RESERVE_AUTO_APPROVED`, `RESERVE_APPROVED`, `RESERVE_REJECTED`, `RESERVE_RETRACTED`) written to `ClaimAuditLog` via `IAuditLogService`, so that the full financial history of a claim is reconstructable from the audit log alone.
15. As the `ApiIntegrationTests` project, I want to exercise the full auto-approve path, the full manual-approve path (two different users, one submits one approves), and the self-approval-rejected path, against the real SQL Server container, so that the authority-threshold and self-approval rules are proven against real persisted state, not mocks.

## Implementation Decisions

- **MediatR commands**: `OpenReserveCommand` (also used for adjustments — §10.2 lists one endpoint for both open and adjust, distinguished by `transactionType` in the body), `ApproveReserveCommand`, `RejectReserveCommand`, `RetractReserveCommand`. **Query**: `ListClaimReservesQuery`.
- **Authority threshold check** happens inside `OpenReserveCommand`'s handler (not a FluentValidation rule, since it needs to know the caller's role, which isn't request-body data): ≤ $10,000 → create the `ReserveHistory` row with `ApprovalStatus = AutoApproved`, immediately update `CurrentAmount`, write the outbox message. > $10,000 → create with `ApprovalStatus = PendingApproval`, do not touch `CurrentAmount`, do not write an outbox message yet (that happens on approval).
- **Self-approval check (BR-R-03)**: `ApproveReserveCommand`'s handler compares the authenticated caller's user id against the `ReserveHistory` row's `SubmittedByUserId`; a match throws the business-rule exception (→ `422`, `blockingConditions`, per Slice B's exception-handling middleware).
- **Balance update + outbox write are one transaction**: both the `ReserveHistory`/`ClaimReserveComponent` update and the `OutboxMessage` insert happen through the same `IUnitOfWork.CommitChangesAsync()` call (ADR-0003), so either both land or neither does.
- **$10M aggregate limit (BR-R-05)**: checked at approval time (not creation time, since the limit is on *approved* totals) by summing `CurrentAmount` across all of the claim's reserve components. If exceeded and the claim's override flag isn't set, the approval is blocked with a `blockingConditions` entry directing the caller to set the override (a separate, Manager-only action — the exact shape of "set override flag" is a small addition to `TransitionClaimStatusCommand`'s claim or a dedicated command, decided during implementation since `REQUIREMENTS.md` doesn't specify a dedicated endpoint for it).
- **Role enforcement** happens in the command handler (or a MediatR pipeline behavior reading `[Authorize]`-equivalent role requirements), checked against the real JWT claims established in Slice B — this is the backend half of `FR-ROLE-01`; the frontend half is out of scope for any backend slice.
- **Reserve creation requires a linked policy**: `OpenReserveCommand`'s validator checks `Claim.PolicyId is not null`, consistent with BR-C-06's stated consequence.

## Testing Decisions

- **Seam: `ClaimsModule.ApiIntegrationTests`**, same reasoning as Slice B — one HTTP-level test proves auth, validation, the handler, persistence, and audit logging together.
- The authority-threshold boundaries (exactly $10,000, exactly $100,000) deserve explicit boundary tests, not just one value comfortably inside each tier.
- The self-approval and the $10M-override scenarios are exactly the kind of "trickiest correctness area" ADR-0009 anticipated reserving space for — give them their own clearly-named test methods, not folded into a single large "reserve approval" test.
- Prior art: Slice B's `ApiFixture`-based tests establish token-minting-per-role and response-shape assertion patterns this slice reuses directly (submitting as `handler`, approving as a *different* `supervisor` user, asserting 422 when the same user tries both).

## Out of Scope

- The outbox dispatcher that reads the `OutboxMessage` rows this slice writes, and the GL posting Hangfire job itself — Slice D. This slice's "done" state is an `OutboxMessage` row existing after approval; nothing in this slice processes it.
- SLA monitoring — Slice D.
- Document upload — Slice E.
- The frontend's reserve authority-threshold real-time indicator and approve/reject button visibility — frontend concern.

## Further Notes

The CC-01 closure condition from Slice B ("no reserve components with status PendingApproval remain") now has real data to check against once this slice lands — worth re-running Slice B's closure-condition tests after this slice is implemented to confirm they still pass with actual pending/approved reserves in play, not just the empty-reserve-table case Slice B was built against.
