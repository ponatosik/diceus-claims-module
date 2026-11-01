using ClaimsModule.Persistence.Repositories;
using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class UnitOfWorkTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task A_multi_repository_write_commits_atomically()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();
        IUnitOfWork unitOfWork = new UnitOfWork(dbContext);
        IClaimRepository claimRepository = new ClaimRepository(dbContext, unitOfWork);
        IClaimReserveComponentRepository reserveRepository = new ClaimReserveComponentRepository(dbContext, unitOfWork);

        var claim = new Claim
        {
            OrganisationId = OrganisationId,
            ClaimNumber = "CLM-2026-0009001",
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
        };
        claimRepository.Add(claim);

        var reserve = new ClaimReserveComponent
        {
            OrganisationId = OrganisationId,
            Claim = claim,
            Component = ReserveComponentType.Indemnity,
            CurrentAmount = 1000m,
            Status = ReserveComponentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        reserveRepository.Add(reserve);

        await claimRepository.CommitChangesAsync();

        await using var readContext = CreateDbContext();
        Assert.True(await readContext.Claims.AnyAsync(c => c.ClaimId == claim.ClaimId));
        Assert.True(await readContext.ClaimReserveComponents.AnyAsync(c => c.ReserveComponentId == reserve.ReserveComponentId));
    }

    [Fact]
    public async Task A_failure_partway_through_a_multi_repository_write_rolls_back_everything()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();
        IUnitOfWork unitOfWork = new UnitOfWork(dbContext);
        IClaimRepository claimRepository = new ClaimRepository(dbContext, unitOfWork);
        IClaimReserveComponentRepository reserveRepository = new ClaimReserveComponentRepository(dbContext, unitOfWork);

        var claim = new Claim
        {
            OrganisationId = OrganisationId,
            ClaimNumber = "CLM-2026-0009002",
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
        };
        claimRepository.Add(claim);

        // Deliberately invalid: points at a claim that doesn't exist, forcing an FK violation
        // partway through the same unit of work.
        var orphanedReserve = new ClaimReserveComponent
        {
            OrganisationId = OrganisationId,
            ClaimId = Guid.NewGuid(),
            Component = ReserveComponentType.Indemnity,
            CurrentAmount = 1000m,
            Status = ReserveComponentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        reserveRepository.Add(orphanedReserve);

        await Assert.ThrowsAsync<DbUpdateException>(() => claimRepository.CommitChangesAsync());

        await using var readContext = CreateDbContext();
        Assert.False(await readContext.Claims.AnyAsync(c => c.ClaimId == claim.ClaimId));
    }

    [Fact]
    public async Task A_claim_with_loss_event_parties_and_risk_objects_commits_atomically_via_navigation()
    {
        // Proves US13's own worked example: "Claim + LossEvent + Parties + RiskObjects + an optional
        // initial reserve commit atomically in one transaction" — via Claim's navigation collections,
        // since ClaimId is a database-generated key not known until SaveChanges runs.
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();
        IUnitOfWork unitOfWork = new UnitOfWork(dbContext);
        IClaimRepository claimRepository = new ClaimRepository(dbContext, unitOfWork);

        var claim = new Claim
        {
            OrganisationId = OrganisationId,
            ClaimNumber = "CLM-2026-0009003",
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
            LossEvent = new LossEvent
            {
                OrganisationId = OrganisationId,
                LossDate = DateTimeOffset.UtcNow.AddDays(-1),
                LossDescription = "Rear-ended at a red light, moderate bumper damage.",
                CauseOfLossCode = "COL-VEH-COL",
                ReportDate = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            },
        };
        claim.Parties.Add(new ClaimParty
        {
            OrganisationId = OrganisationId,
            PartyRole = PartyRole.Claimant,
            PartyType = PartyType.Person,
            FirstName = "Alex",
            LastName = "Rivera",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        claim.RiskObjects.Add(new ClaimRiskObject
        {
            OrganisationId = OrganisationId,
            AssetType = AssetType.Vehicle,
            AssetDescription = "2021 Honda Civic",
            IsPrimary = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        claimRepository.Add(claim);

        await claimRepository.CommitChangesAsync();

        await using var readContext = CreateDbContext();
        Assert.True(await readContext.Claims.AnyAsync(c => c.ClaimId == claim.ClaimId));
        Assert.True(await readContext.LossEvents.AnyAsync(e => e.ClaimId == claim.ClaimId));
        Assert.True(await readContext.ClaimParties.AnyAsync(p => p.ClaimId == claim.ClaimId && p.PartyRole == PartyRole.Claimant));
        Assert.True(await readContext.ClaimRiskObjects.AnyAsync(r => r.ClaimId == claim.ClaimId));
    }

    private ClaimsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options);
}
