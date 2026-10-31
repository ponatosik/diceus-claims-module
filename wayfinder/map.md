---
id: 0
title: "Backend Implementation Decision Log"
labels: ["wayfinder:map"]
---

## Destination

A **complete decision log** for the backend's architecture/implementation choices — every place `REQUIREMENTS.md` deliberately leaves discretion to the engineer, resolved and recorded (as an ADR where the decision is hard to reverse, surprising, or a real trade-off; as a plain ticket resolution otherwise) — so that actual backend coding can proceed without re-litigating design decisions mid-implementation. This map does **not** cover Azure deployment topology or CI/CD pipeline design (§19–20 of REQUIREMENTS.md) — those are a separate future effort. It also does not cover frontend architecture.

## Notes

- Domain: DICEUS Claims Module backend — .NET 9, C# 13, Clean Architecture, CQRS via MediatR, EF Core 9, FluentValidation, AutoMapper, Hangfire. See `REQUIREMENTS.md` (authoritative requirements baseline) and `CLAUDE.md` (planned repo layout + cross-cutting architecture notes) at the repo root.
- Every ticket on this map is `wayfinder:grilling` (HITL conversation) — this effort is pure architecture decision-making with the person who owns the project, not research or prototyping.
- Call the `domain-modeling` skill when resolving a ticket whose answer is hard to reverse, surprising, or a real trade-off — write the ADR in `docs/adr/` immediately, per that skill's format.
- Tracker: this repo has no external issue tracker; see `wayfinder/TRACKER.md` for the local-markdown convention in use (file = issue, frontmatter for labels/assignee/blocked_by).

## Decisions so far

- [Claim number generation strategy](tickets/001-claim-number-generation.md): single never-resetting SQL `SEQUENCE`, created via migration — see [ADR-0001](../docs/adr/0001-claim-number-sequence.md)
- [Reserve balance (CurrentAmount) persistence strategy](tickets/002-reserve-balance-persistence.md): persisted column, sequential delta tracking via `ReserveHistory.PreviousBalance`/`NewBalance`, updated only on final approval — see [ADR-0002](../docs/adr/0002-reserve-balance-sequential-delta.md)
- [Repository granularity and Unit of Work shape](tickets/003-repository-unit-of-work-pattern.md): per-aggregate repositories, explicit `IUnitOfWork` owns commit logic, repositories' `CommitChangesAsync()` is a thin pass-through — see [ADR-0003](../docs/adr/0003-repository-unit-of-work.md)
- [Domain event dispatch mechanism, and Hangfire's data store](tickets/004-domain-events-outbox.md): in-process MediatR notifications for same-DB effects; outbox pattern bridges reserve approval into Hangfire's **separate** SQL Server store — see [ADR-0004](../docs/adr/0004-domain-events-outbox-for-hangfire.md)
- [GL posting job idempotency mechanism](tickets/005-gl-posting-idempotency.md): application check-then-insert as primary path, DB unique constraint as safety net; constraint violation = graceful no-op, not a retry — see [ADR-0005](../docs/adr/0005-gl-posting-idempotency.md)
- [Exception-to-HTTP-status-code mapping](tickets/006-exception-status-code-mapping.md): `422` reserved for validation/business-rule failures only; not-found → `404`, concurrency conflict → `409`, unhandled → `500` — see [ADR-0006](../docs/adr/0006-exception-to-status-code-mapping.md)
- [JWT validation strategy and fake issuer choice](tickets/007-jwt-auth-strategy.md): real `AddJwtBearer` validation against a self-hosted, test-only token issuer; issuer/keys fully configuration-driven — see [ADR-0007](../docs/adr/0007-jwt-auth-self-hosted-test-issuer.md)
- [CorrelationId source for in-request audit entries](tickets/008-correlation-id-source.md): sourced from OpenTelemetry's `Activity.Current` trace id; exporter/collector config deferred as a deployment concern
- [Validation-severity pipeline handling and non-field-keyed business-rule error shape](tickets/009-validation-severity-and-business-rule-error-shape.md): new `POST /api/claims/validate` query endpoint; generic severity-aware `ValidationBehavior` (blocks only on `Severity.Error`); handlers write `VALIDATION_ISSUE_ADDED` for attached warnings; `422` body gets a second `blockingConditions` array (structured `{code, message}`) for non-field business-rule failures — see [ADR-0008](../docs/adr/0008-validation-severity-and-business-rule-error-shape.md)
- [Testing strategy: project layout, unit/integration boundaries, Testcontainers images](tickets/010-testing-strategy-boundaries.md): three test projects by cost tier (UnitTests, PersistenceTests, ApiIntegrationTests); one SQL Server container (2 DBs) + one Azurite container per assembly; Respawn for isolation; automated Clean Architecture fitness test — see [ADR-0009](../docs/adr/0009-testing-strategy.md)

## Not yet specified

- AutoMapper profile organization (one profile per aggregate vs. per feature) — too minor to ticket yet; will likely just get decided inline during implementation rather than needing its own decision ticket.
- Hangfire recurring-job registration/startup wiring specifics (where `RecurringJob.AddOrUpdate` calls live, how local dev wires the separate Hangfire connection string) — small enough that it may not need a ticket; revisit if it turns out thornier than expected once ticket 4's outbox dispatcher is designed in detail.

## Out of scope

- **Azure deployment topology and CI/CD pipeline design** (§19–20) — explicitly deferred to a separate future effort per this map's destination; not fog, just not this map's job.
- **Soft-delete cascading rules** — ruled out: no claim-deletion workflow exists anywhere in the business rules (claims only move through the status lifecycle, never get deleted), so there's nothing to cascade. `IsDeleted`/soft-delete columns stay as schema convention per §16.1 but no cascading behavior needs designing.
- **API versioning** — ditched. The implementation follows `REQUIREMENTS.md` §10's route paths exactly, with no version segment or header introduced, rather than adopting URL-segment or header-based versioning.
- **Frontend architecture** — a separate map if/when that planning effort starts.
