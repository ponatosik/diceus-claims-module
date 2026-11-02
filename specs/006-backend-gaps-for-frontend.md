---
title: "Slice F: Backend Gaps for the Frontend"
labels: ["ready-for-agent"]
status: open
---

## Problem Statement

The Angular screens in `REQUIREMENTS.md` §11 cannot be built faithfully against the API as it stands (Slices A–E). The dashboard needs a multi-status filter, a handler text search, and a Cause of Loss column; the FNOL form needs to submit an initial reserve atomically; the Claim Detail screen needs severity, notes, handler, pending reserve amounts, a GL retry action, a closure pre-flight checklist, and user/entity information on audit entries. Nothing lets a browser on another origin call the API, and the deployed demo has no way to obtain a token (the dev issuer 404s outside Development/Testing and mints a random `userId` per token).

## Solution

Close every gap in the backend — not with frontend workarounds — before any frontend slice starts, keeping every change inside the existing CQRS / validation / audit / outbox patterns. Auth changes follow [ADR-0010](../docs/adr/0010-dev-token-issuer-flag-and-fixed-demo-users.md).

## User Stories

1. As the Angular frontend served from a different origin, I want CORS configured from an allow-list in configuration (`Cors:AllowedOrigins`), so that the deployed Static Web App can call the API while arbitrary origins cannot. (Local dev uses the `ng serve` proxy and needs no CORS entry.)
2. As a demo deployment operator, I want `POST /api/dev/auth/token` gated on `DevAuth:Enabled` (default `false`) rather than environment name, so that the public demo can authenticate without running as `Development` (ADR-0010).
3. As a reviewer switching between Demo Users, I want exactly three fixed users — handler, supervisor, manager — with stable Guids and display names, seeded as a small user directory, and the token endpoint to accept `{ userId }` for a demo user (rejecting unknown ids with 400, taking the role from the directory, and returning `{ accessToken, userId, displayName, role }`), so that Submitter-only Retract and self-approval rejection (`BR-R-03`) behave consistently across sessions.
4. As a dashboard user, I want `GET /api/claims` to accept `status` as a repeated query parameter (`?status=Open&status=Closed`), so that the §11.1 multi-select status filter works with server-side pagination.
5. As a dashboard user, I want `GET /api/claims` to accept `assignedHandler` (partial, case-insensitive match on the handler's display name via the user directory), so that §11.1's "assigned handler (text search)" works. The existing `assignedHandlerId` stays.
6. As a dashboard user, I want `ClaimSummaryDto` to include `CauseOfLossCode` and `CauseOfLossName` (the reference table's `Name` column), so that the §11.1 Cause of Loss column can render.
7. As a FNOL user, I want `CreateClaimCommand` to accept an optional `InitialReserve { component, amount, changeReason }`, persisted in the same transaction as the claim (authority tier, `PendingApproval` routing, GL outbox enqueue and audit exactly as `OpenReserveCommand`), so that §11.2 Step 3 is atomic and the README / spec 002 statement that FNOL accepts an initial reserve becomes true.
8. As a Claim Detail user, I want `ClaimDetailDto` to include `Severity` (nullable), `Notes`, `AssignedHandler { userId, displayName }` and `CauseOfLossName`, so that the §11.3 header and Overview tab can render.
9. As a handler, I want `PATCH /api/claims/{id}/notes` (`UpdateClaimNotesCommand`, body `{ notes }`) writing a `CLAIM_NOTES_UPDATED` audit entry with old/new values, so that §11.3's editable claim notes field has something to save to.
10. As a Reserves-tab user, I want `ReserveSummaryDto` to include `PendingAmount` (sum of that component's `PendingApproval` transactions), so that §11.3's summary cards show Current Balance and Pending Amount without client-side derivation.
11. As a Reserves-tab user, I want `POST /api/claims/{id}/reserves/{txnId}/retry-posting` (`RetryGLPostingCommand`) that is only valid for a transaction with `PostingStatus = Failed`, resets it to `Pending`, writes an outbox message re-enqueuing the GL job with the **same** idempotency key, and writes a `GL_POSTING_RETRY_REQUESTED` audit entry, so that §11.3's retry button for Failed rows works without breaking ADR-0005's idempotency.
12. As a Reserves-tab and audit user, I want `ReserveTransactionDto` to carry `SubmittedByDisplayName` and `ApprovedByDisplayName` (and `RejectedByDisplayName`), and `ClaimDocumentDetailDto` to carry `UploadedByDisplayName`, resolved from the user directory, so that "Submitted By" / "Approved By" / uploader show names.
13. As an Audit-tab user, I want `AuditLogEntryDto` to include `AuditLogId`, `UserId`, `UserDisplayName`, `RelatedEntityType` and `RelatedEntityId`, so that §11.3's "user" and "related entity link" can render.
14. As a user about to transition a claim, I want `GET /api/claims/{id}/status/preflight?target={status}` (`GetTransitionPreflightQuery`) returning `{ isTransitionValid, blockingConditions[], warnings[] }`, reusing `ClaimTransitionConditions` exactly as the real transition does, so that the Closed confirmation dialog renders a named CC-01–CC-04 checklist before the user commits.

## Implementation Decisions

- **User directory**: a seeded `Users` reference table (`UserId`, `DisplayName`, `Role`) in the main DB with an `IUserDirectory` read interface in Application; fixed Guids defined once and shared with test fixtures. Not tenant-scoped beyond the existing `OrganisationId` convention. No user management endpoints.
- **Display-name resolution** happens in the query handlers / mappers (single batched lookup per response), not via navigation properties from business tables — user ids on business rows stay plain `UNIQUEIDENTIFIER`s.
- **Initial reserve reuse**: the reserve-opening logic currently inside `OpenReserveCommandHandler` is extracted into a domain/application service both handlers call, so authority, aggregate-limit (`BR-R-05`), outbox and audit behavior cannot diverge. Its FluentValidation rules are composed into `CreateClaimCommandValidator` (so `POST /api/claims/validate` reports initial-reserve issues too, routed by `InitialReserve.*` property names).
- **Preflight** is a Query (no state change, no audit). For an invalid target it returns `isTransitionValid = false` plus the valid next statuses as a blocking condition, mirroring the 422 body of the real transition. CC-04 surfaces as a warning with code `CC-04`.
- **Retry-posting** goes through the outbox (ADR-0004), never calls Hangfire directly from the request. Any authenticated role may retry — §11.3 shows the button without a role restriction.
- **CORS** policy registered in API startup; allowed origins from configuration only, no wildcard, credentials not needed (Bearer header).
- `CC-*`/`BR-*` codes and the existing `422` shape (ADR-0008) are unchanged.

## Testing Decisions

- **Seam: `ClaimsModule.ApiIntegrationTests`** for every new/changed endpoint (same "one seam" approach as Slices B–E): multi-status and handler-name filtering; FNOL with an auto-approved initial reserve (outbox row exists) and with a supervisor-tier one (`PendingApproval`, no outbox row); notes update + audit; retry-posting on a Failed row (Pending + outbox with the original key) and its 422 on a non-Failed row; preflight returning CC-01/CC-03/CC-04 for a seeded claim; DTO display names.
- Dev token endpoint: 404 when `DevAuth:Enabled=false`, token for a known demo user id when enabled, 400 for an unknown id.
- CORS: an allowed origin gets `Access-Control-Allow-Origin`; a disallowed one does not.
- Unit tests only for the extracted reserve-opening service's pure branches if extraction exposes any; architecture tests must keep passing.

## Out of Scope

- Severity assignment rules or a severity-editing endpoint — the spec defines neither (see Further Notes).
- Real identity provider integration; user management.
- Column sorting on the claims list.

## Further Notes

- `Claim.Severity` is never set by any code path and `REQUIREMENTS.md` gives no assignment rule; it is exposed as nullable and the UI shows "Not assessed". This is a deliberate refusal to invent behavior, to be explained in the live review.
- Update `README.md`'s endpoint list and env-var table (`Cors:AllowedOrigins`, `DevAuth:Enabled`) when this slice lands.
