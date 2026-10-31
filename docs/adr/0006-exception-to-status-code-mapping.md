# Exception-handling middleware maps exception type to HTTP status; 422 is reserved for validation only

§10.5 specifies a structured `422` JSON error shape for validation failures but doesn't say what every other failure looks like. We use a single exception-handling middleware that maps exception *type* to the status code that actually fits it: `ValidationException` (FluentValidation) and domain validation/business-rule violations (e.g. self-approval, invalid status transition) → `422` with the structured `errors` shape from §10.5; an entity-not-found exception → `404`; an optimistic-concurrency (`RowVer`) conflict → `409`; anything unhandled → `500`. **422 is not a catch-all** — it is reserved specifically for validation-shaped failures.

## Considered Options

- **Everything funneled through 422** for simplicity (one error shape everywhere). Rejected: conflates "the request was malformed/violates a business rule" with "the resource doesn't exist" or "someone else changed this first," which are semantically different and which API consumers (including the Angular frontend's snackbar error handling, §11.4) should be able to distinguish.

## Consequences

Every custom domain exception type needs an explicit mapping entry in the middleware (or a shared base type per status-code family, e.g. `NotFoundException`, `ConflictException`, `DomainValidationException`) — an exception type that's added later without updating the mapping silently falls through to `500`.
