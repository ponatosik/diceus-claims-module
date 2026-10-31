# Real JWT validation against a self-hosted, test-only token issuer; issuer is configuration-driven

§11.4 only requires simulating JWT auth ("a hardcoded mock auth service is acceptable"). We go further: the API performs **real** `AddJwtBearer` signature/expiry/audience/issuer validation — nothing about token *validation* is faked. What's fake is the token *source*: a minimal, self-hosted token-issuing endpoint, used **for local development and testing only**, mints JWTs signed with a dev key matching the API's configured issuer/audience. The trusted issuer, authority, and signing key material are all read from configuration (`appsettings.json`/environment), so swapping in a real identity provider for an actual deployment requires a config change, not a code change — mirroring the `IStorageService` provider-abstraction pattern already used for blob storage.

## Considered Options

- **Keycloak (Docker container)**: most realistic stand-in for a real enterprise IdP, common in insurance/.NET-adjacent shops, but adds a heavier container and configuration surface to a take-home's local dev setup for no validation-fidelity benefit over a minimal issuer. Rejected for this effort; noted as the natural upgrade if a more realistic IdP integration ever needs demonstrating.
- **Duende IdentityServer / OpenIddict**: the ".NET-native" choices. Rejected for the same reason as Keycloak — more infrastructure than a minimal signed-JWT issuer buys here, plus Duende's licensing is a needless complication for a test-only issuer.

## Consequences

Nobody should mistake the self-hosted issuer for production-appropriate auth — it must be clearly scoped to local/test environments (e.g. only wired up in a `Development`/`Testing` configuration or docker-compose profile), with the real deployed service expected to point its configuration at an actual trusted issuer instead.
