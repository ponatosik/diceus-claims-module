---
id: 9
title: "Validation-severity pipeline handling and non-field-keyed business-rule error shape"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

Two related open questions about the validation/error pipeline that ADR-0006 (exception-to-status-code mapping) didn't settle:

1. §5.4/§8 categorize validation issues as **Critical** (blocking) vs **Warning** (non-blocking — the claim may still proceed, e.g. "loss date outside policy effective period"). FluentValidation has a built-in `Severity` concept (`WithSeverity(Severity.Warning)`). Does the MediatR validation pipeline behavior use that directly — rejecting the request only on `Critical`-severity failures, while `Warning`-severity failures pass through and get attached to the response/persisted as advisory info (e.g. a `VALIDATION_ISSUE_ADDED` audit entry per the event type list in §14.1)? What carries a Warning from the pipeline behavior into the response body and into the audit log?
2. The `422` shape in §10.5 is field-keyed (`"errors": {"LossDate": ["..."]}`), which fits FluentValidation naturally. But some business-rule failures aren't field-shaped at all — e.g. closure-condition failures (§4.3/BR-ST-03) are a list of named conditions ("no reserve components with status PendingApproval remain", etc.), not a single field's error. Does the `422` response get a second shape for this case (e.g. a `blockingConditions` array alongside or instead of `errors`), or do these get force-fit into the field-keyed shape with a synthetic field name?

## Notes

Related to [ticket 6 (closed)](006-exception-status-code-mapping.md) and [ADR-0006](../../docs/adr/0006-exception-to-status-code-mapping.md), which settled *which* exceptions get 422 but not the internal shape variance within 422 responses.

## Resolution

Resolved via a grilling session (6 questions across 2 rounds). Summary:

1. Added `POST /api/claims/validate` — a MediatR `Query`, shares `CreateClaimCommandValidator`, always `200`, returns every Critical + Warning issue, fills the FRS §5.4 prose gap.
2. Severity uses FluentValidation's native `Severity` enum directly (`Error` = Critical, `Warning` = Warning) — no custom enum.
3. One generic `ValidationBehavior<TRequest, TResponse>` blocks (→ 422, `errors`) only on `Severity.Error`; Warning-severity failures attach to the command via a shared `IHasValidationWarnings` marker interface instead of being audited by the behavior itself.
4. Handlers (not the pipeline behavior) write `VALIDATION_ISSUE_ADDED` audit entries for attached warnings, after persisting, since creation commands have no `ClaimId` to audit against until then.
5. The `422` body gets a second array, `blockingConditions` (structured `{code, message}` using existing `BR-*`/`CC-*` rule IDs), for non-field business-rule failures (closure conditions, invalid transitions, self-approval rejection) — feeding §11.3's pre-flight checklist UI.
6. `errors` comes from FluentValidation via the pipeline behavior; `blockingConditions` comes from handler-thrown domain exceptions caught by the same middleware as ADR-0006.

Full reasoning, rejected alternatives, and the edge-case consequence (two exception types feeding one response shape) in [ADR-0008](../../docs/adr/0008-validation-severity-and-business-rule-error-shape.md).
