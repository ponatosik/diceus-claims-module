using ClaimsModule.Domain;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class ClaimTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task A_saved_claim_round_trips_with_a_generated_id_and_row_version()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();

        var claim = new Claim
        {
            OrganisationId = OrganisationId,
            ClaimNumber = "CLM-2026-0000001",
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
        };

        dbContext.Claims.Add(claim);
        await dbContext.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, claim.ClaimId);
        Assert.NotNull(claim.RowVer);
        Assert.NotEmpty(claim.RowVer);
    }

    [Fact]
    public async Task A_soft_deleted_claim_is_excluded_from_the_default_query()
    {
        await fixture.ResetAsync();
        await using (var dbContext = CreateDbContext())
        {
            dbContext.Claims.Add(new Claim
            {
                OrganisationId = OrganisationId,
                ClaimNumber = "CLM-2026-0000002",
                Status = ClaimStatus.Draft,
                ReportedDate = DateTimeOffset.UtcNow,
                IsDeleted = true,
            });
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = CreateDbContext();
        var claims = await readContext.Claims
            .Where(c => c.ClaimNumber == "CLM-2026-0000002")
            .ToListAsync();

        Assert.Empty(claims);
    }

    [Fact]
    public async Task A_claim_from_a_different_organisation_is_excluded_from_the_default_query()
    {
        await fixture.ResetAsync();
        var otherOrganisationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await using (var writeContext = CreateDbContext(otherOrganisationId))
        {
            writeContext.Claims.Add(new Claim
            {
                OrganisationId = otherOrganisationId,
                ClaimNumber = "CLM-2026-0000003",
                Status = ClaimStatus.Draft,
                ReportedDate = DateTimeOffset.UtcNow,
            });
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateDbContext(OrganisationId);
        var claims = await readContext.Claims
            .Where(c => c.ClaimNumber == "CLM-2026-0000003")
            .ToListAsync();

        Assert.Empty(claims);
    }

    [Fact]
    public async Task PolicyId_is_a_real_foreign_key_to_the_policy_table()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();

        dbContext.Claims.Add(new Claim
        {
            OrganisationId = OrganisationId,
            ClaimNumber = "CLM-2026-0000004",
            PolicyId = Guid.NewGuid(), // does not exist in the seeded Policies table
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private ClaimsDbContext CreateDbContext() => CreateDbContext(OrganisationId);

    private ClaimsDbContext CreateDbContext(Guid organisationId) =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options)
        {
            CurrentOrganisationId = organisationId,
        };
}
