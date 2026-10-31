using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class ClaimsDbContextTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task ClaimsDbContext_can_connect_to_the_container()
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;

        await using var dbContext = new ClaimsDbContext(options);

        var canConnect = await dbContext.Database.CanConnectAsync();

        Assert.True(canConnect);
    }

    [Fact]
    public async Task ResetAsync_runs_without_throwing_against_an_empty_schema()
    {
        // No tables exist yet (no entities implemented) — this proves the Respawn wiring
        // itself works and degrades gracefully, ahead of real schema/tests landing.
        await fixture.ResetAsync();
    }
}
