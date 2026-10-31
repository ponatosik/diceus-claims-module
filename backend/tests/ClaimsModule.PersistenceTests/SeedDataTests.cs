using ClaimsModule.Domain;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class SeedDataTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task All_ten_cause_of_loss_codes_from_section_5_6_are_seeded_and_active()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();

        var codes = await dbContext.CauseOfLossCodes.OrderBy(c => c.SortOrder).ToListAsync();

        Assert.Equal(10, codes.Count);
        Assert.All(codes, c => Assert.True(c.IsActive));

        var expected = new[]
        {
            ("COL-FIRE", "Fire", PerilCategory.Property),
            ("COL-FLOOD", "Flood", PerilCategory.Weather),
            ("COL-THEFT", "Theft", PerilCategory.Crime),
            ("COL-VEH-COL", "Vehicle Collision", PerilCategory.Auto),
            ("COL-VEH-COMP", "Vehicle Comprehensive", PerilCategory.Auto),
            ("COL-LIAB", "Third Party Liability", PerilCategory.Liability),
            ("COL-EQUIP", "Equipment Breakdown", PerilCategory.Equipment),
            ("COL-WIND", "Wind / Storm", PerilCategory.Weather),
            ("COL-INJURY", "Bodily Injury", PerilCategory.Liability),
            ("COL-OTHER", "Other / Unknown", PerilCategory.General),
        };

        foreach (var (code, name, perilCategory) in expected)
        {
            var match = codes.SingleOrDefault(c => c.Code == code);
            Assert.NotNull(match);
            Assert.Equal(name, match.Name);
            Assert.Equal(perilCategory, match.PerilCategory);
        }
    }

    [Fact]
    public async Task All_five_policies_from_section_5_5_are_seeded()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();

        var policies = await dbContext.Policies.OrderBy(p => p.PolicyNumber).ToListAsync();

        Assert.Equal(5, policies.Count);

        var archived = policies.Single(p => p.PolicyNumber == "POL-2023-000099");
        Assert.Equal("Archived Corp", archived.ClientName);
        Assert.Equal(new DateOnly(2020, 1, 1), archived.EffectiveDate);
        Assert.Equal(new DateOnly(2021, 12, 31), archived.ExpirationDate);

        var meridian = policies.Single(p => p.PolicyNumber == "POL-2024-001001");
        Assert.Equal("Meridian Transport LLC", meridian.ClientName);
        Assert.Equal(new DateOnly(2024, 1, 1), meridian.EffectiveDate);
        Assert.Equal(new DateOnly(2026, 12, 31), meridian.ExpirationDate);
    }

    private ClaimsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options);
}
