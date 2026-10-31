---
id: 7
title: "JWT validation strategy and fake issuer choice"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

§11.4 only requires simulating JWT auth ("a hardcoded mock auth service is acceptable"). Should validation itself be real or faked, and if a fake token issuer is used, which one — a self-hosted minimal issuer, or a more realistic IdP container (Keycloak, Duende IdentityServer, OpenIddict)?

## Resolution

Validation is **real** — genuine `AddJwtBearer` signature/expiry/audience/issuer checks, nothing faked there. What's fake is the token *source*: a minimal, self-hosted token-issuing endpoint, **scoped strictly to local dev/test environments**, mints JWTs signed with a dev key matching the API's configured issuer/audience. Issuer/authority/signing-key configuration is read from `appsettings.json`/environment — mirroring the `IStorageService` provider-abstraction pattern — so a real deployed service can point at an actual trusted IdP via config alone, no code change. Keycloak/Duende/OpenIddict considered and rejected as disproportionate infrastructure for a take-home's local setup; noted as the natural upgrade path. Full reasoning in [ADR-0007](../../docs/adr/0007-jwt-auth-self-hosted-test-issuer.md).
