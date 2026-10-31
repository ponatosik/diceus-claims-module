---
id: 10
title: "Testing strategy: project layout, unit/integration boundaries, Testcontainers images"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

xUnit + Testcontainers are the chosen tools (settled during destination-naming), but the shape isn't decided:

- Test project layout: one test project per Clean Architecture layer (`ClaimsModule.Application.Tests`, `ClaimsModule.Persistence.IntegrationTests`, `ClaimsModule.API.IntegrationTests`), or a different split?
- Unit vs. integration boundary: which layers get pure unit tests (fast, no containers — e.g. domain logic, FluentValidation validators, MediatR handlers with mocked repositories) vs. which get Testcontainers-backed integration tests (e.g. EF Core configuration/migrations against a real SQL Server, full API request/response cycles)?
- Which Testcontainers images are actually needed: SQL Server (for EF Core/Persistence), Azurite (for Azure Blob Storage integration, given `IStorageService` has an `AzureBlobStorageService` implementation to verify), anything for the self-hosted test JWT issuer (ticket 7) or the separate Hangfire SQL Server store (ticket 4)?
- Does the GL posting job's idempotency behavior (ticket 5) get its own dedicated integration test suite, given it's one of the trickiest pieces of correctness (duplicate-key handling, re-entrancy)?

## Notes

Depends conceptually on the outcomes of [ticket 4 (closed, outbox/Hangfire)](004-domain-events-outbox.md), [ticket 5 (closed, idempotency)](005-gl-posting-idempotency.md), and [ticket 7 (closed, JWT)](007-jwt-auth-strategy.md) for *what* needs integration coverage, though those are already resolved — this ticket is about *how* testing is organized, not re-litigating those.

## Resolution

Resolved via a grilling session (7 questions across 2 rounds). Summary:

1. Three test projects by cost tier: `ClaimsModule.UnitTests` (pure, existing), `ClaimsModule.PersistenceTests` (Testcontainers SQL Server, schema-level, no HTTP), `ClaimsModule.ApiIntegrationTests` (Testcontainers SQL Server + Azurite, full HTTP via `WebApplicationFactory`).
2. Container topology: one SQL Server container with two databases (main + Hangfire-store stand-in) + one Azurite container, scoped per test project via an xUnit collection fixture — not solution-wide, not per-test.
3. No container for JWT — tokens minted in-process via ADR-0007's self-hosted issuer, called directly by the test fixture.
4. GL posting idempotency gets a dedicated test class inside `ApiIntegrationTests`, not a fourth project.
5. Added an automated Clean Architecture fitness test (`NetArchTest.Rules`) to `ClaimsModule.UnitTests`, enforcing the Domain/Application dependency rules at build time.
6. Containers scoped per assembly (not shared across `PersistenceTests`/`ApiIntegrationTests`) — simpler, no cross-assembly lifecycle coordination.
7. State isolation via Respawn (reset both databases before each test), not database-per-test-class or transaction-rollback — rollback specifically breaks the cross-database GL-posting/outbox scenarios this strategy exists to cover.

Full reasoning, rejected alternatives, and the Docker-requirement consequence in [ADR-0009](../../docs/adr/0009-testing-strategy.md).
