---
title: "Slice B: FNOL, Claim Reads, Parties, Status Transitions, Reference Data"
labels: ["ready-for-agent"]
status: open
---

## Problem Statement

A claims handler has no way to record a First Notice of Loss, look up existing claims, manage the parties on a claim, or move a claim through its lifecycle — none of that exists yet. This is also the first slice that needs a working request pipeline at all: nothing yet validates a request, authenticates a caller, maps an exception to the right HTTP status, or writes an audit trail entry. Every later slice (reserves, background jobs, documents) assumes all of that plumbing already works.

## Solution

Implement the FNOL intake command, the claim read queries, party management, the status-transition state machine, and the reference-data/policy-search endpoints from `REQUIREMENTS.md` §10.1/§10.3/§10.4 — and, because this is the first real feature, stand up the shared infrastructure every one of those endpoints needs: real JWT validation against a self-hosted test issuer (ADR-0007), the generic severity-aware `ValidationBehavior` with the `blockingConditions` error shape (ADR-0008), the exception-to-status-code middleware (ADR-0006), `IAuditLogService`, and request-scoped `CorrelationId` propagation (wayfinder ticket 8).

## User Stories

1. As a claims handler, I want to submit a new claim via `POST /api/claims` with loss details, at least one claimant party, and optional risk objects and an initial reserve, so that a FNOL record is created transactionally with a generated `ClaimNumber`.
2. As a claims handler, I want the system to reject claim creation with HTTP 422 when the loss date is in the future (BR-C-01), so that obviously-wrong intake data never gets persisted.
3. As a claims handler, I want a Warning (not a block) when the loss date falls outside the linked policy's effective period (BR-C-02), so that the claim can still be created but the issue is visible.
4. As a claims handler, I want claim creation blocked with 422 if no `ClaimParty` of role `Claimant` is supplied (BR-C-03), so that every claim has someone to pay.
5. As a claims handler, I want the `CauseOfLossCode` validated against active reference data (BR-C-05), so that an inactive or nonexistent code is rejected before the claim is created.
6. As a claims handler, I want to create a claim without a known policy (`PolicyId = null`) and get a Warning rather than a block (BR-C-06), so that FNOL intake isn't blocked by missing policy information, while reserve creation later stays blocked until a policy is linked.
7. As a claims handler, I want loss description validation requiring at least 20 characters (BR-C-07) as a Critical issue, so that empty or trivial descriptions can't slip through.
8. As a claims handler filling out the FNOL form, I want `POST /api/claims/validate` to return every Critical and Warning issue for my in-progress data without persisting anything, so that I can see problems before I submit (fills the FRS §5.4 prose gap per ADR-0008).
9. As a claims handler, I want claim numbers formatted `CLM-{YYYY}-{7-digit-seq}` and guaranteed unique even under concurrent submissions, so that no two claims ever collide (ADR-0001, built on Slice A's sequence).
10. As a claims handler, I want `GET /api/claims` to return a paginated, filterable list (status, date range, assigned handler, cause of loss, free-text search) with summary fields including `totalReserves`, so that I can triage my open claims.
11. As a claims handler, I want `GET /api/claims/{id}` to return the full claim detail — parties, risk objects, reserve summary, document list, recent audit entries — so that the Angular Claim Detail screen has everything it needs in one call.
12. As a claims handler, I want `GET /api/claims/{id}/audit` to return the paginated, reverse-chronological, append-only audit log for a claim, so that I can see everything that's happened to it.
13. As a claims handler, I want `POST /api/claims/{id}/parties` to add a party and `DELETE /api/claims/{id}/parties/{partyId}` to soft-remove one, so that I can manage who's involved in a claim after intake.
14. As a claims handler, I want removing the last `Claimant` party rejected with 422 (BR-P-01), so that a claim can never end up with zero claimants.
15. As a claims handler, I want `PUT /api/claims/{id}/status` to transition a claim's status, validated strictly against the state machine in §4.2, so that any transition not explicitly listed returns 422 with the list of valid next statuses (BR-ST-01).
16. As a claims handler, I want transitioning to `Open` blocked unless there are no Critical validation issues and at least one Claimant exists (BR-ST-02), so that incomplete claims can't advance.
17. As a claims handler, I want transitioning to `Closed` blocked unless all four closure conditions (CC-01 through CC-04) are satisfied, with the specific unmet conditions returned as structured `blockingConditions` (ADR-0008), so that I know exactly what's stopping closure — including the CC-04 case where open reserves require an explicit justification note to proceed.
18. As a claims supervisor, I want transitioning to `Reopened` to require the Supervisor role and a non-empty reason (BR-ST-04), so that reopening a closed claim is deliberate and attributable.
19. As a claims handler, I want every significant action (claim created, status changed, party added/removed) to write an append-only `ClaimAuditLog` entry via a single `IAuditLogService` — never a direct `DbContext` write from a handler (AUD-02) — so that the audit trail is centralized and trustworthy.
20. As a claims handler submitting data that produces a Warning-severity validation issue, I want a `VALIDATION_ISSUE_ADDED` audit entry written once the claim is actually persisted (ADR-0008), so that the warning is part of the permanent record even though it didn't block creation.
21. As a frontend developer, I want `GET /api/reference/cause-of-loss-codes` (optionally filtered by peril category) and `GET /api/reference/claim-statuses` (with each status's valid next transitions) to serve static/seeded reference data, so that the FNOL form and status-transition UI can populate their dropdowns.
22. As a frontend developer, I want `GET /api/policies/search` (by policy number or client name) and `GET /api/policies/{id}/coverage` to query the seeded policy data from Slice A, so that the FNOL form's policy typeahead works.
23. As any API caller, I want every request to carry a Bearer JWT that's genuinely validated (signature, expiry, audience, issuer) against configuration, so that authentication is real even though the token issuer is a self-hosted test-only endpoint locally (ADR-0007).
24. As any API caller, I want a validation failure to return the field-keyed `errors` shape from §10.5, a non-field business-rule failure to return the `blockingConditions` shape from ADR-0008, a not-found resource to return 404, and a `RowVer` conflict to return 409 — never a generic 500 for something the API should have handled (ADR-0006).
25. As a DICEUS reviewer inspecting the audit log, I want every audit entry written within one HTTP request to carry the same `CorrelationId`, sourced from `Activity.Current`'s trace id, so that a request's full set of side effects is traceable together (wayfinder ticket 8).
26. As the `ClaimsModule.ApiIntegrationTests` project, I want to mint a valid JWT in-process (no separate issuer process/container) for whichever role a test needs, so that integration tests can exercise role-gated behavior without real user interaction.

## Implementation Decisions

- **MediatR commands**: `CreateClaimCommand`, `AddClaimPartyCommand`, `RemoveClaimPartyCommand`, `TransitionClaimStatusCommand`. **Queries**: `ValidateClaimQuery` (new, per ADR-0008 — a `Query`, never throws, always 200, reuses `CreateClaimCommandValidator`), `ListClaimsQuery`, `GetClaimDetailQuery`, `GetClaimAuditLogQuery`, `ListCauseOfLossCodesQuery`, `ListClaimStatusesQuery`, `SearchPoliciesQuery`, `GetPolicyCoverageQuery`.
- **Validation pipeline (ADR-0008)**: one generic `ValidationBehavior<TRequest, TResponse>` wired at the MediatR pipeline level (not controller-level `[ApiController]` validation — §18.1 reviewers check this explicitly). It throws only on `Severity.Error` (FluentValidation's default = Critical); `Severity.Warning` failures attach to the command via a shared `IHasValidationWarnings` marker interface. The handler, not the behavior, writes `VALIDATION_ISSUE_ADDED` audit entries for attached warnings, once a persisted `ClaimId` exists.
- **Error shapes**: FluentValidation failures → `422` with the field-keyed `errors` dict (§10.5). Closure-condition failures, invalid-transition failures, and other non-field business-rule violations → `422` with a second `blockingConditions` array of `{code, message}` objects, using the existing `BR-*`/`CC-*` rule IDs as codes. These come from a dedicated domain exception type thrown inside handlers, caught by the same exception-handling middleware as everything else.
- **Exception-handling middleware (ADR-0006)**: one middleware maps exception type → status code. `ValidationException`/business-rule exceptions → 422; an entity-not-found exception → 404; an EF Core `DbUpdateConcurrencyException` (RowVer mismatch) → 409; anything unhandled → 500.
- **Status state machine**: the transition table from §4.2 and the closure conditions from §4.3 are encoded as static application logic (per Slice A's decision not to persist `ClaimStatusTransitions` as a table). `TransitionClaimStatusCommand`'s handler checks the current status against the allowed-transitions map before doing anything else.
- **JWT auth (ADR-0007)**: `AddJwtBearer` performs real signature/expiry/audience/issuer validation. Issuer/authority/signing-key configuration is read from `appsettings.json`/environment. A minimal self-hosted token-issuing endpoint (scoped to `Development`/`Testing` configuration only) mints test tokens for the three roles (`handler`, `supervisor`, `manager`). `ClaimsModule.ApiIntegrationTests`' `ApiFixture` mints tokens in-process by calling the same signing logic directly — no HTTP round-trip to a separate issuer.
- **`IAuditLogService`**: the sole write path to `ClaimAuditLog` (AUD-02) — no handler touches the audit table via `DbContext` directly. It reads `CorrelationId` from `Activity.Current`'s trace id (wayfinder ticket 8) and stamps it on every entry written during the request.
- **Party removal validation (BR-P-01)**: `RemoveClaimPartyCommand`'s handler checks whether the target party is the claim's last active `Claimant` before soft-removing it; if so, it throws the business-rule exception that resolves to the `blockingConditions` shape.
- **Reference data and policy search** are plain read queries against Slice A's seeded `CauseOfLossCode`/`Policy` tables and the static status-transition map — no caching layer, no write path.

## Testing Decisions

- **Seam: `ClaimsModule.ApiIntegrationTests`**, one seam per the "ideal number is one" principle — a single HTTP request/response cycle through `ApiFixture` exercises JWT auth, the validation pipeline, the handler, persistence, and audit logging together. Good tests assert on the HTTP response (status code, body shape) and on observable side effects (a row exists in `ClaimAuditLog`, a `ReserveHistory`/`ClaimReserveComponent` row from an optional initial reserve) — never on internal handler call sequencing.
- Cover the full matrix of BR-C/BR-ST/BR-P rules as named test cases, not just the happy path — each business rule in `REQUIREMENTS.md` §7.1/§7.3/§7.5 should map to at least one test that proves it's enforced and one that proves the boundary (e.g. exactly 20-character loss description passes, 19 fails).
- `ClaimsModule.UnitTests` is reserved for anything genuinely awkward to reach via HTTP — e.g. the status-transition map's pure lookup logic, if it's complex enough to warrant isolated coverage beyond what the HTTP-level closure/transition tests already prove.
- Prior art: `SwaggerTests.cs` already establishes the `ApiFixture` + `[Collection(ApiCollection.Name)]` pattern this slice's tests extend.

## Out of Scope

- Reserve creation, approval, rejection, retraction — Slice C. (This slice's closure-condition check for CC-01/CC-04 queries the reserve tables Slice A already created, but no command in this slice writes to them.)
- GL posting, the outbox dispatcher, SLA monitoring — Slice D.
- Document upload/download — Slice E.
- Role-gated UI behavior — frontend concern, not this slice (this slice only needs the backend to genuinely enforce roles server-side, per `FR-ROLE-01`).
- A production-grade identity provider — ADR-0007 explicitly scoped the self-hosted issuer to local/test use; swapping in a real IdP for an actual deployment is a future, out-of-map concern.

## Further Notes

This is the largest slice and the one most other slices' tests will structurally resemble — getting the `ApiFixture`-based test shape right here (JWT minting, response-shape assertions, audit-log assertions) sets the pattern Slices C, D, and E will all follow rather than reinvent.
