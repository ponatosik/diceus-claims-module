using ClaimsModule.Persistence.Repositories;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class ClaimRepositoryTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task ListStaleAsync_matches_status_and_falls_back_to_CreatedAt_when_UpdatedAt_is_null()
    {
        await fixture.ResetAsync();
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = CreateDbContext();
        dbContext.Claims.Add(new Claim // Open, never modified, CreatedAt 50h ago -> stale
        {
            OrganisationId = OrganisationId,
            ClaimNumber = $"CLM-2026-{Random.Shared.Next(1, 9999999):D7}",
            Status = ClaimStatus.Open,
            ReportedDate = now,
            CreatedAt = now.AddHours(-50),
        });
        dbContext.Claims.Add(new Claim // Open, updated 1h ago -> not stale
        {
            OrganisationId = OrganisationId,
            ClaimNumber = $"CLM-2026-{Random.Shared.Next(1, 9999999):D7}",
            Status = ClaimStatus.Open,
            ReportedDate = now,
            CreatedAt = now.AddHours(-100),
            UpdatedAt = now.AddHours(-1),
        });
        dbContext.Claims.Add(new Claim // Closed, created 100h ago -> excluded by status, not by staleness
        {
            OrganisationId = OrganisationId,
            ClaimNumber = $"CLM-2026-{Random.Shared.Next(1, 9999999):D7}",
            Status = ClaimStatus.Closed,
            ReportedDate = now,
            CreatedAt = now.AddHours(-100),
        });
        await dbContext.SaveChangesAsync();

        var repository = new ClaimRepository(dbContext, new UnitOfWork(dbContext));
        var stale = await repository.ListStaleAsync([ClaimStatus.Draft, ClaimStatus.Open], now.AddHours(-48));

        Assert.Single(stale);
        Assert.Equal(ClaimStatus.Open, stale[0].Status);
        Assert.Null(stale[0].UpdatedAt);
    }

    private ClaimsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options);
}
