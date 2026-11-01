using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class ClaimNumberGeneratorTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Concurrent_calls_never_produce_the_same_claim_number()
    {
        await fixture.ResetAsync();

        const int concurrentCalls = 25;
        var tasks = Enumerable.Range(0, concurrentCalls).Select(async _ =>
        {
            var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options;
            await using var dbContext = new ClaimsDbContext(options);
            IClaimNumberGenerator generator = new SqlSequenceClaimNumberGenerator(dbContext);
            return await generator.GenerateAsync();
        });

        var claimNumbers = await Task.WhenAll(tasks);

        Assert.Equal(concurrentCalls, claimNumbers.Distinct().Count());
    }

    [Fact]
    public async Task Claim_number_matches_the_required_format()
    {
        await fixture.ResetAsync();

        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var dbContext = new ClaimsDbContext(options);
        IClaimNumberGenerator generator = new SqlSequenceClaimNumberGenerator(dbContext);

        var claimNumber = await generator.GenerateAsync();

        Assert.Matches(@"^CLM-\d{4}-\d{7}$", claimNumber);
    }
}
