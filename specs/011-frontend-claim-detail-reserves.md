---
title: "Slice K: Claim Detail — Reserves Tab"
labels: ["ready-for-agent"]
status: open
blocked_by: ["010"]
---

## Problem Statement

Users cannot see reserve balances or history, open/adjust reserves, or approve, reject, retract or retry them from the UI (§11.3 Tab 3, §6). Role gating of approval must exist in the frontend independently of the backend (FR-ROLE-01).

## Solution

A Reserves tab backed by `GET /api/claims/{id}/reserves` (loaded on first open) with summary cards, a transaction history table with role-aware row actions, an Add Reserve slide-in panel, and GL posting status that updates by polling while any row is Pending.

## User Stories

1. As a user, I want one summary card per component showing Current Balance and Pending Amount, amber when an amount is pending approval (§11.3).
2. As a user, I want a transaction history table: Date | Type | Component | Amount (green for increase, red for decrease) | Status badge | Submitted By | Approved By, plus a GL Posting Status badge (Pending | Posted | Failed) per row (§11.3).
3. As a user, I want an "Add Reserve" button opening a slide-in panel (Material sidenav/drawer) with Component, Amount, Reason and the real-time authority indicator (§11.3). The panel picks the transaction type itself: **Add** (positive amount) when the component doesn't exist yet, **Adjust** (signed delta, with a "new balance will be X" preview) when it does.
4. As a user, I want a "Reverse" action on each component card that submits a Reverse transaction zeroing that component's balance, after confirmation.
5. As a Supervisor or Manager, I want Approve / Reject buttons on every PendingApproval row (§11.3, FR-ROLE-01) — never rendered for handlers — and **disabled with an explanatory tooltip** when the row's Authority Tier is above my role (supervisor on a Manager-tier row) or when I am its Submitter ("You can't approve your own reserve — BR-R-03").
6. As a Supervisor or Manager, I want Reject to require a rejection reason in a dialog (`POST …/reject { rejectionReason }`).
7. As a Submitter, I want a Retract button on my own PendingApproval rows only (matched by Demo User id) (§11.3).
8. As a user, I want a Retry button on Failed GL posting rows calling `POST …/reserves/{txnId}/retry-posting` (§11.3).
9. As a Manager, I want an "Approve aggregate-limit override" action — visible only to managers — when a pending transaction would take the claim's total over $10,000,000 or an approval fails with a `BR-R-05` blocking condition, calling `POST …/reserves/override`; once set, the tab shows an "Override approved" badge (§3, BR-R-05).
10. As a user, I want GL posting status to update without a manual refresh: the store polls the reserves list every 5s while any row is `Pending`, and stops when none are.
11. As a user, after any reserve action I want reserves, header and audit re-fetched (Slice J pattern), and 422 `blockingConditions` (e.g. BR-R-03, BR-R-05) shown in the panel/dialog that triggered them.
12. As a user arriving from an audit link (`?tab=reserves&highlight=<reserveHistoryId>`), I want the referenced row scrolled into view and highlighted.

## Implementation Decisions

- Increase/decrease colouring uses the sign of `Amount`; for `SubrogationRecoverable` the same sign rule applies (no inversion — §11.3 doesn't specify one).
- Approve/Reject visibility: `canApproveReserves()` (supervisor|manager). Enablement: `canApproveTier(authorityTier(row.amount))` && `row.submittedByUserId !== currentUser.id`. The backend enforces both independently.
- Authority indicator in the panel and the tier on rows use the same shared `authorityTier` helper as FNOL, from `|amount|` — identical to the backend's `ReserveAuthority.GetTier`.
- Polling is an RxJS `timer` inside the store, started/stopped by a computed "has pending postings" signal and torn down with the route.

## Testing Decisions

- **Unit**: transaction-type selection (Add vs Adjust), new-balance preview, approve-enablement matrix (role × tier × self-submitted), polling start/stop.
- **Component**: handler sees no Approve/Reject; supervisor sees them disabled on a Manager-tier row and on own submission; Retract only on own pending rows; Retry only on Failed rows; manager-only override action.
- **E2E**: handler opens a $25,000 reserve → PendingApproval, no Approve button for handler; switch to supervisor → approve → row Approved, GL status reaches Posted via polling. Supervisor submits $50,000 → own row's Approve disabled; a direct API approve call returns 422 `BR-R-03`.

## Delivery Notes

- Decreases and reversals needed a backend change first: BR-R-01's "> 0" now governs opening a
  component (Add); Adjust is a signed non-zero delta and Reverse exactly zeroes the balance, with no
  non-SubrogationRecoverable balance below zero (§6.2) — [ADR-0011](../docs/adr/0011-reserve-decreases-br-r-01-scope.md).
- The GL posting badge appears only on approved rows: a transaction awaiting approval has nothing
  enqueued, so its stored `Pending` posting status would mislead.
- Polling runs only while an *approved* transaction's posting is Pending; in docker-compose the outbox
  dispatcher runs once a minute, so Pending → Posted typically shows within ~1 minute.
- Row actions without their own dialog (Approve, Retry) surface a 422's Blocking Condition through the
  error interceptor's snackbar; the Add Reserve panel shows it inline.

## Out of Scope

- Claim payments (§2); editing a PendingApproval transaction (must be retracted and resubmitted, §6.4).
