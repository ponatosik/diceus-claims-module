---
title: "Slice A: Domain & Persistence Foundation"
labels: ["ready-for-agent"]
status: open
---

## Problem Statement

Right now the backend solution is an empty Clean Architecture skeleton: `ClaimsModule.Domain` has no entities, `ClaimsDbContext` has no `DbSet`s, and there is no migration. Every subsequent feature (FNOL intake, reserve management, background jobs, document management) needs a real, persisted domain model to build against — handlers can't be written, validators can't check against real data, and `ClaimsModule.PersistenceTests`/`ClaimsModule.ApiIntegrationTests` can't exercise anything meaningful until the schema exists.

## Solution

Implement the full Claims Management domain model end to end: the Domain entities and enums, the Persistence layer's EF Core Fluent configurations and `DbContext` wiring, the first migration, and the reference-data seeding — all matching the data conventions in `REQUIREMENTS.md` §16 and the architecture decisions already locked in `docs/adr/0001`–`0004`. This is pure foundation work: no MediatR commands/queries, no API endpoints, no business-rule validation. It exists so every later slice has real tables, real entities, and a working repository/Unit-of-Work pattern to build on.

## User Stories

1. As a developer implementing claim intake, I want a `Claim` entity with all the fields `REQUIREMENTS.md` §9.1 specifies (`ClaimNumber`, `PolicyId`, `Status`, `Severity`, `ReportedDate`, `AssignedHandlerId`, `ClosedAt`, `ClosureReason`, `Notes`, full audit columns, `RowVer`), so that I can persist a claim once FNOL intake is implemented.
2. As a developer implementing claim intake, I want a `LossEvent` entity linked 1:1 to a `Claim` (§9.2), so that the loss date, description, location, cause-of-loss code, estimated amount, report date, and police report number are all persisted.
3. As a developer implementing claim intake, I want a `ClaimParty` entity (§9.3) supporting both `Person` and `Company` party types with a `PartyRole` enum (`Claimant`, `Insured`, `ThirdParty`, `Witness`, `Attorney`) and an `IsActive` soft-remove flag, so that parties can be added to and soft-removed from a claim.
4. As a developer implementing claim intake, I want a `ClaimRiskObject` entity (§9.4) with an `AssetType` enum (`Vehicle`, `Property`, `Person`, `Equipment`, `Other`) and an `IsPrimary` flag, so that damaged assets can be linked to a claim.
5. As a developer implementing reserve management, I want a `ClaimReserveComponent` entity (§9.5) with a `Component` enum (`Indemnity`, `Expense`, `ALAE`, `SubrogationRecoverable`) and a persisted `CurrentAmount`, so that each claim can carry multiple simultaneous reserve components.
6. As a developer implementing reserve management, I want a `ReserveHistory` entity (§9.6) with `TransactionType`, `Amount`, `PreviousBalance`, `NewBalance`, `ApprovalStatus`, approval/rejection audit fields, `ChangeReason`, `PostingStatus`, `PostingJobId`, `IdempotencyKey`, and `ChangeSequence`, so that every reserve transaction is recorded append-only per ADR-0002's sequential-delta design.
7. As a developer implementing document upload, I want a `ClaimDocument` entity (§9.7) with `DocumentType`, `DocumentName`, `BlobPath`, `ContentType`, `FileSizeBytes`, and uploader metadata, so that document metadata can be persisted independently of the blob storage provider.
8. As a developer implementing audit logging, I want a `ClaimAuditLog` entity (§9.8) with `EventType`, `Description`, `OldValue`/`NewValue` (JSON), `RelatedEntityId`/`RelatedEntityType`, `CorrelationId`, and `CreatedByUserId`, so that `IAuditLogService` has somewhere to write append-only audit entries.
9. As a developer implementing claim intake, I want a `CauseOfLossCode` reference entity (§9.9) seeded with the 10 codes from §5.6, so that claim creation can validate against real, active reference data.
10. As a developer implementing the simulated Policy domain, I want a `Policy` reference entity (§9.10) seeded with the 5 policies from §5.5, so that policy search and claim-to-policy linkage work against real data.
11. As a developer implementing the outbox-bridged GL posting flow (ADR-0004), I want an `OutboxMessage` entity/table, so that reserve approval can atomically record "a GL posting job must eventually be enqueued" in the same transaction as the `ReserveHistory` write.
12. As a developer running `dotnet ef database update` against a fresh SQL Server instance, I want every table to follow the column conventions in §16.1 (`UNIQUEIDENTIFIER` PKs via `NEWSEQUENTIALID()`, `DECIMAL(19,4)` money, `DATETIMEOFFSET(7)` UTC timestamps, full audit columns, `OrganisationId` tenant isolation, `RowVer` on aggregate roots), so that the schema is consistent and migration-reproducible with no manual setup.
13. As a developer writing a MediatR command handler, I want one repository per aggregate root and a shared `IUnitOfWork` that each repository's `CommitChangesAsync()` delegates to (ADR-0003), so that multi-entity writes (e.g. Claim + LossEvent + Parties + RiskObjects + an optional initial reserve) commit atomically in one transaction.
14. As a developer writing a claim-creation handler, I want an atomically-incrementing claim-number source backed by a single, never-resetting SQL `SEQUENCE` (ADR-0001), so that concurrent FNOL submissions never collide on `ClaimNumber`.
15. As a developer writing a reserve-approval handler, I want `ClaimReserveComponents.CurrentAmount` to be a persisted column I update via sequential delta tracking rather than computing a live `SUM()` (ADR-0002), so that balance history and current state stay consistent without an aggregate query on every read.
16. As a reviewer checking Clean Architecture adherence, I want every EF Core global query filter (soft delete, tenant isolation) applied centrally in `IEntityTypeConfiguration<T>` classes rather than scattered `WHERE` clauses, so that §16.2's conventions are enforced structurally, not by convention alone.
17. As the `ClaimsModule.PersistenceTests` project, I want the migration to apply cleanly against a fresh Testcontainers SQL Server instance with no manual setup, so that CI and local development both start from the same reproducible state.

## Implementation Decisions

- **Entities and enums live in `ClaimsModule.Domain`**: `Claim`, `LossEvent`, `ClaimParty`, `ClaimRiskObject`, `ClaimReserveComponent`, `ReserveHistory`, `ClaimDocument`, `ClaimAuditLog`, `CauseOfLossCode`, `Policy`, and `OutboxMessage`. Enums: `ClaimStatus`, `PartyRole`, `PartyType`, `AssetType`, `ReserveComponentType`, `ReserveTransactionType`, `ReserveApprovalStatus`, `ReservePostingStatus`, `PerilCategory`, `PolicyStatus`. `Domain` has zero dependency on EF Core, matching `ClaimsModule.UnitTests/ArchitectureTests.cs`'s existing enforcement.
- **`ClaimStatusTransitions` is not a database table.** The two source documents disagree here — the FRS's detailed entity section (§9) never lists it, while the Assessment Brief's condensed summary table (§3.2) does. Since the transition rules are a small, fixed set (§4.2's table) with no requirement anywhere for them to be configurable per organisation, they're encoded as a static lookup in application code (next slice), not persisted. `GET /api/reference/claim-statuses` (built in Slice B) serves this same static data.
- **EF Core configuration lives in `ClaimsModule.Persistence`** as one `IEntityTypeConfiguration<T>` class per entity (§18.1's reviewer checklist explicitly checks for Fluent API configuration, not data annotations). A shared base/extension method applies the soft-delete global query filter (`HasQueryFilter(e => !e.IsDeleted)`) and tenant isolation consistently across every configuration rather than repeating it per entity.
- **Claim number generation**: one SQL `SEQUENCE` object, created via migration, read through an atomic `NEXT VALUE FOR` call — never read-then-increment in application code. Per ADR-0001, the sequence never resets per year; `{YYYY}` in the rendered `ClaimNumber` is just the creation year.
- **`ClaimReserveComponents.CurrentAmount`** is a real persisted column. It is **not** touched by this slice's seed/migration logic beyond existing as a column — the sequential-delta update logic (read `PreviousBalance`, compute `NewBalance`, write both to `ReserveHistory`, write `NewBalance` to `CurrentAmount`) is reserve-management handler logic, built in Slice C. This slice only needs the schema to support it.
- **Repository + Unit of Work (ADR-0003)**: one repository interface/implementation per aggregate root (`IClaimRepository`, `IClaimReserveComponentRepository`, etc.), all constructed against the same scoped `ClaimsDbContext`. An explicit `IUnitOfWork` type owns `SaveChangesAsync`; each repository's `CommitChangesAsync()` is a thin pass-through delegating to the shared `IUnitOfWork` instance. No handler injects `IUnitOfWork` directly — only repositories.
- **Outbox table (`OutboxMessage`)**: `Id`, `Type` (discriminates message kind, string), `Payload` (JSON, `NVARCHAR(MAX)`), `CreatedAt`, `ProcessedAt` (nullable), plus the standard audit/tenant columns. Built now so Slice C (reserve approval) and Slice D (the outbox dispatcher) both have a real table to write to and read from; this slice does not implement the dispatcher itself.
- **Seed data**: the 10 `CauseOfLossCode` rows from §5.6 and the 5 `Policy` rows from §5.5, applied via EF Core `HasData`/migration-time inserts (§16.4) — never at application startup. A single fixed `OrganisationId` GUID is seeded alongside and referenced by configuration, not hardcoded in handler code later.
- **`dotnet ef migrations add`/`database update`** target `ClaimsModule.Persistence` as the migrations project and `ClaimsModule.API` as the startup project, matching `CLAUDE.md`'s documented commands.

## Testing Decisions

- **Seam: `ClaimsModule.PersistenceTests`, using `DatabaseFixture`** (already scaffolded). No HTTP layer exists in this slice, so this is the highest seam that exercises anything real.
- Good tests here check **external behavior of the schema and configuration**, not EF Core's own internals: does the migration apply cleanly against a fresh container; does a round-tripped entity actually come back with the conventions applied (e.g. a soft-deleted `Claim` is excluded from a default query, a decimal column round-trips at `(19,4)` precision without silent truncation, `NEXT VALUE FOR` the claim-number sequence never returns the same value twice under concurrent calls); does seed data exist and match §5.5/§5.6 exactly.
- Prior art: `ClaimsDbContextTests.cs` already establishes the pattern (`DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString)`, `[Collection(DatabaseCollection.Name)]`, `fixture.ResetAsync()` before tests that need a clean slate).
- The repository/Unit-of-Work atomicity claim (a multi-entity write either fully commits or fully rolls back) is exactly the kind of thing worth a dedicated `PersistenceTests` case: force a failure partway through a multi-repository write and assert nothing persisted.

## Out of Scope

- Any MediatR command/query, any API controller/endpoint, any FluentValidation validator — those belong to Slices B–E.
- The outbox dispatcher's actual polling/enqueue logic — Slice D.
- Reserve approval's balance-update logic — Slice C.
- Hangfire's own storage schema (a separate database per ADR-0004) — Slice D sets that up when it wires Hangfire itself.
- Document blob storage (`IStorageService`) — Slice E.

## Further Notes

This slice is the one place in the whole backend where `REQUIREMENTS.md` §9 (entity definitions) and §16 (data conventions) need to be read together, line by line, rather than skimmed — every subsequent slice assumes this schema is correct and won't re-derive it. If anything here turns out wrong, every later slice's migrations/tests inherit the mistake.
