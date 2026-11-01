using ClaimsModule.Application.Common;
using ClaimsModule.Domain;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class AuditableEntitySaveChangesInterceptorTests(DatabaseFixture fixture)
{
    private static readonly Guid FixedUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Stamps_OrganisationId_CreatedAt_and_UserCreated_on_insert()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();

        var claim = new Claim
        {
            ClaimId = Guid.NewGuid(),
            ClaimNumber = "CLM-2026-0000900",
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
        };

        dbContext.Claims.Add(claim);
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var saved = await readContext.Claims.SingleAsync(c => c.ClaimId == claim.ClaimId);

        Assert.Equal(SeedData.OrganisationId, saved.OrganisationId);
        Assert.True(saved.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(FixedUserId, saved.UserCreated);
    }

    [Fact]
    public async Task Stamps_UpdatedAt_and_UserModified_on_update()
    {
        await fixture.ResetAsync();
        var claimId = Guid.NewGuid();

        await using (var writeContext = CreateDbContext())
        {
            writeContext.Claims.Add(new Claim
            {
                ClaimId = claimId,
                ClaimNumber = "CLM-2026-0000901",
                Status = ClaimStatus.Draft,
                ReportedDate = DateTimeOffset.UtcNow,
            });
            await writeContext.SaveChangesAsync();
        }

        await using (var updateContext = CreateDbContext())
        {
            var claim = await updateContext.Claims.SingleAsync(c => c.ClaimId == claimId);
            claim.Status = ClaimStatus.Open;
            await updateContext.SaveChangesAsync();
        }

        await using var readContext = CreateDbContext();
        var saved = await readContext.Claims.SingleAsync(c => c.ClaimId == claimId);

        Assert.NotNull(saved.UpdatedAt);
        Assert.True(saved.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(FixedUserId, saved.UserModified);
    }

    private ClaimsDbContext CreateDbContext()
    {
        var interceptor = new AuditableEntitySaveChangesInterceptor(new FakeCurrentUserService());
        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new ClaimsDbContext(options) { CurrentOrganisationId = SeedData.OrganisationId };
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => FixedUserId;

        public string? Role => "handler";
    }
}
