---
status: accepted (amends ADR-0007's "Development/Testing only" consequence)
---

# Dev token issuer gated by an explicit config flag; three fixed demo users with stable ids

The Angular frontend must ship to a publicly reachable Azure deployment (§19) with a mock login and role switcher (§11.4), but the API validates real signed JWTs (ADR-0007) — so the browser cannot mint its own tokens, and the dev issuer gated on `IsDevelopment()`/`Testing` would leave the deployed demo unable to authenticate at all. We gate `POST /api/dev/auth/token` on an explicit `DevAuth:Enabled` flag (default `false`, turned on only for local dev, tests, and the demo deployment) instead of the environment name, and replace the "random userId per token" behavior with **three fixed demo users** (handler, supervisor, manager) with stable Guids and display names, seeded server-side as a small user directory. Stable ids are what make submitter-only Retract and self-approval rejection (`BR-R-03`) work across sessions; the directory supplies display names for Submitted By / Approved By / audit user and backs the dashboard's handler text filter.

## Considered Options

- **Pre-minted long-lived tokens baked into the frontend bundle.** Rejected: tokens expire or become effectively permanent credentials in a public bundle, and the signing key/issuer can't rotate without a rebuild.
- **Keep environment-name gating, deploy the API as `Development`.** Rejected: drags Development-only behavior (detailed errors etc.) into the public deployment just to unlock one endpoint.

## Consequences

ADR-0007's guardrail ("never mistaken for production auth") now rests on the flag being off by default and documented in README as demo-only, not on environment naming. Pointing the API at a real IdP remains a config change; the frontend's `MockAuthService` is the only piece that would be replaced.
