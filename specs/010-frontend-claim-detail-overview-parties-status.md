---
title: "Slice J: Claim Detail — Header, Overview, Parties, Status Transitions"
labels: ["ready-for-agent"]
status: open
blocked_by: ["007"]
---

## Problem Statement

After creating or selecting a claim, users cannot view it, manage its parties, edit notes, or move it through the status lifecycle (§11.3 header, Tab 1, Tab 2; §4.2–4.3).

## Solution

A `claim-detail` route with a `ClaimDetailStore` (provided at the route) that loads `GET /api/claims/{id}` for the always-visible header and the Overview/Parties tabs, and lazily loads the remaining tabs' data on first open (Slices K, L). A transition menu driven by the reference claim-status table, with confirmation dialogs and a Pre-flight Check for Closed.

## User Stories

1. As a user, I want a header always visible across tabs with Claim Number (prominent, copy-to-clipboard), status chip (shared `StatusBadge` colours), Policy Number and Client Name, Loss Date and Cause of Loss, and Assigned Handler (§11.3).
2. As a user, I want a transition button/menu listing the valid next statuses for the current status, from `GET /api/reference/claim-statuses` (cached once per session) (§11.3); `Reopened → Open` is automatic and never offered.
3. As a handler, I want Reopen hidden from me, since it requires Supervisor or Manager (BR-ST-04) — the UI gates exactly what the backend gates and nothing more.
4. As a user, I want every transition to open a confirmation dialog; Withdrawn and Reopen require a reason (free text, as the backend accepts).
5. As a user transitioning to Closed, I want the dialog to show a pre-flight checklist from `GET /api/claims/{id}/status/preflight?target=Closed`: each of CC-01–CC-03 as ✓/✗ with its message, confirm disabled while any blocking condition fails, and CC-04 (open reserves) shown as a warning that makes a justification note required before confirming (§4.3, §11.3).
6. As a user, if a transition still fails with a 422, I want its `blockingConditions` shown in the dialog rather than only as a snackbar.
7. As a user, on the **Overview** tab I want loss event details (date, description, location, cause), a Severity badge ("Not assessed" when null), the estimated loss amount, and an editable claim notes field saved via `PATCH /api/claims/{id}/notes` (§11.3).
8. As a user, on the **Parties** tab I want every party listed with role badge, name, contact details and active/inactive status, an "Add Party" inline form (`POST /api/claims/{id}/parties`), and a Remove button per party (`DELETE …/parties/{partyId}`) that is disabled for the last active Claimant (§11.3).
9. As a user, after any mutation I want the header, the affected tab and the audit log re-fetched from the server rather than patched locally, so that server projections stay authoritative.
10. As a user opening a non-existent claim id, I want the not-found page (Slice G).

## Implementation Decisions

- Tabs are child routes or `mat-tab-group` with a `tab` query param (deep-linkable, required by Slice L's audit entity links): `?tab=overview|parties|reserves|documents|audit&highlight=<entityId>`.
- `ClaimDetailStore` exposes signals per resource with `load`/`reload` methods; mutations call the API service then `reload` the affected resources + header + audit.
- The "last active Claimant" disablement is computed from loaded parties; the backend's 422 remains the guarantee.
- Party form reuses the FNOL party row component from Slice I (shared under `features/claim-detail` imports or moved to `shared/` when reuse happens).

## Testing Decisions

- **Unit**: available-transitions helper (reference table + role → offered statuses, Reopen excluded for handler, Reopened→Open never offered); store reload-after-mutation behavior.
- **Component**: Closed dialog renders preflight items, disables confirm on a failing CC, requires justification when CC-04 is present; Remove disabled for last Claimant.
- **E2E**: handler sees no Reopen on a Closed claim; supervisor reopens with a reason and the claim shows Open.

## Out of Scope

- Editing loss details, risk objects, severity, or handler assignment (not in §11.3).
