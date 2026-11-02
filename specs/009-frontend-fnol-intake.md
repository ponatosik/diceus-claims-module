---
title: "Slice I: FNOL Intake Wizard"
labels: ["ready-for-agent"]
status: open
blocked_by: ["007"]
---

## Problem Statement

Handlers cannot log a new claim from the UI. §11.2 requires a three-step reactive form with per-step validation, a policy typeahead with in-force indication, parties and risk objects, an optional initial reserve with a live authority indicator, and a review step.

## Solution

A Material stepper (linear) over one reactive `FormGroup`, with client-side validators mirroring the Critical rules for instant feedback and `POST /api/claims/validate` as the authoritative check on each **Next** and before **Create**, submitting via `POST /api/claims` (with the optional `InitialReserve` added in Slice F).

## User Stories

1. As a handler, in **Step 1 — Policy & Loss Details**, I want a policy typeahead (`GET /api/policies/search?q=`, debounced, ≥2 chars) whose selection populates policy number, client name and effective dates (§11.2), and shows that policy's coverage types read-only from `GET /api/policies/{id}/coverage` (§10.4).
2. As a handler, I want an In-force badge beside the selected policy — green when the loss date is within the effective period, amber when the policy is expired or the loss date is outside it — recomputed whenever the policy or loss date changes (§11.2).
3. As a handler, I want an "Unknown Policy" toggle that clears and disables the policy search and submits `policyId: null` (§11.2).
4. As a handler, I want a Loss Date date-time picker (Material datepicker + timepicker) rejecting future dates; a searchable Cause of Loss dropdown from `/api/reference/cause-of-loss-codes`; a Loss Description textarea with a live "n / 20 minimum" counter; free-text Loss Location; optional Estimated Loss Amount (§11.2).
5. As a handler, in **Step 2 — Parties & Risk Objects**, I want "Add Party" to open an inline form row (role, Person/Company, name fields per type, email, phone) and "Add Risk Object" to open an inline row (asset type, asset description, optional damage description, optional reference number), with added items shown as cards/chips with a remove button (§11.2).
6. As a handler, I want an inline warning when no Claimant has been added (blocking) and an advisory when no risk object has been added (non-blocking) (§11.2).
7. As a handler, in **Step 3 — Initial Reserve & Review**, I want an optional Component (Indemnity, Expense, ALAE, SubrogationRecoverable) + Amount (+ change reason) section with a real-time authority indicator: "✓ Auto-approved (≤ $10,000)", "⚠ Supervisor approval required", "⚠ Manager approval required" (§11.2), driven by the shared `authorityTier(|amount|)` helper.
8. As a handler, I want a read-only review summary table of everything entered (§11.2).
9. As a handler, I want each step to validate before I can advance: client validators first, then `POST /api/claims/validate` on **Next**, showing server issues whose `PropertyName` belongs to the current or earlier steps inline on the matching controls and in a summary at the top of the step (§11.2).
10. As a handler, I want **Create Claim** disabled until Steps 1 and 2 are valid, and a confirmation dialog listing any Warning-severity issues (from a final validate call) before submission (§11.2).
11. As a handler, on success I want to land on Claim Detail with a snackbar showing the generated Claim Number (§11.2); on a 422 I want the errors shown inline and at the top of the step they belong to, with the stepper moved to the earliest step containing an error (§11.2).

## Implementation Decisions

- **Step routing of server issues** by property-name prefix: Step 1 ← `PolicyId`, `LossDate`, `LossDescription`, `LossLocation`, `CauseOfLossCode`, `EstimatedLossAmount`; Step 2 ← `Parties*`, `RiskObjects*`; Step 3 ← `InitialReserve*`. Unknown property names go to the top-of-form summary.
- Client validators exist for responsiveness only; the server is authoritative (validate + create), so a drifting client rule can never let a bad claim through.
- Form state is a single `FormGroup` with `FormArray`s for parties and risk objects; dropdown values come from the TS enum unions (`PartyRole`, `PartyType`, `AssetType`, `ReserveComponentType`), which mirror the backend enums.
- No draft persistence across reloads — not in §11.2.
- Submit button disabled while the create request is pending (§11.4).

## Testing Decisions

- **Unit**: issue-to-step routing; review-summary mapping from form value to `CreateClaimCommand` payload (including `policyId: null` for Unknown Policy and omission of an empty initial reserve).
- **Component**: Step 1 blocks Next on a future loss date and on a <20-char description; Step 2 shows the claimant warning and blocks Next; authority indicator text at 10,000 / 10,000.01 / 100,000.01; warning-confirmation dialog appears when validate returns warnings.
- **E2E**: full FNOL as handler with a policy and a $5,000 initial reserve → lands on Claim Detail, snackbar shows `CLM-YYYY-NNNNNNN`, reserve is auto-approved.

## Out of Scope

- Coverage evaluation (§2): coverages are displayed, not evaluated.
- Editing a claim's loss details after creation (not in §11.3).
