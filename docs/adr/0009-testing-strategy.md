# Testing strategy: three projects by cost tier, shared containers per assembly, Respawn for isolation

xUnit + Testcontainers were already chosen (destination-naming), but not the shape. We split tests into **three projects by cost tier**: `ClaimsModule.UnitTests` (pure — domain logic, FluentValidation validators via `TestValidate()`, MediatR handlers with mocked repositories, zero I/O, zero containers), `ClaimsModule.PersistenceTests` (Testcontainers SQL Server; EF Core configurations, migrations, soft-delete/tenant-isolation query filters — no HTTP, no MediatR), and `ClaimsModule.ApiIntegrationTests` (Testcontainers SQL Server + Azurite; `WebApplicationFactory`-driven full HTTP request/response cycles through the real MediatR pipeline, including auth).

**Container topology**: one SQL Server container hosting two databases — the main `ClaimsModule` database and a second one standing in for Hangfire's separate store (mirroring [ADR-0004](0004-domain-events-outbox-for-hangfire.md)'s "separate store" design without needing a second SQL Server container) — plus one Azurite container for blob storage. Scoped per test project via an xUnit collection fixture (`PersistenceTests` and `ApiIntegrationTests` each spin up their own containers), not shared solution-wide and not recreated per test. No container is needed for JWT: [ADR-0007](0007-jwt-auth-self-hosted-test-issuer.md)'s self-hosted test issuer is called in-process to mint tokens directly within the test fixture.

GL posting idempotency ([ADR-0005](0005-gl-posting-idempotency.md)) — the trickiest correctness area (duplicate-key handling, re-entrancy, the outbox dispatcher) — gets a dedicated test class inside `ApiIntegrationTests`, not a fourth project, since it needs the real Hangfire store + outbox dispatcher + SQL Server wired together, which only that project has.

Within a shared container, tests reset state via [Respawn](https://github.com/jbogard/Respawn) (both databases emptied before each test) rather than a fresh database per test class or transaction-rollback-per-test — the latter specifically breaks the cross-database scenarios (GL posting, outbox) this strategy exists to cover, since a rolled-back transaction in the main database would never let the outbox dispatcher observe and act on the committed outbox row.

Separately, `ClaimsModule.UnitTests` gets an automated Clean Architecture fitness test (via `NetArchTest.Rules`) asserting `Domain` has zero dependency on EF Core/Hangfire/Azure SDK types, and `Application` has zero dependency on `Persistence`/`Infrastructure`/`API` — turning "Clean Architecture adherence" (35%-weighted in the evaluation criteria, §22) into something the build enforces rather than something a reviewer has to notice by eye.

## Considered Options

- **One shared container across the whole solution's test run** (coordinated across both `PersistenceTests` and `ApiIntegrationTests` assemblies). Rejected: the cross-assembly lifecycle coordination needed to share a Testcontainer safely is disproportionate complexity for a take-home-sized test suite; a bit more container startup time per-project is a cheaper cost to pay.
- **Database-per-test-class or transaction-rollback-per-test** for isolation. Rejected: per-test-class databases add container/DB churn for no extra correctness signal; transaction-rollback breaks observability of anything that needs to actually commit and be read back by a separate process (the outbox dispatcher reading committed rows, Hangfire jobs reading committed `ReserveHistory`).

## Consequences

Running the full test suite now requires Docker (for Testcontainers) in any environment that runs `PersistenceTests` or `ApiIntegrationTests` — `ClaimsModule.UnitTests` alone has no such requirement and should stay runnable without Docker for fast local iteration.
