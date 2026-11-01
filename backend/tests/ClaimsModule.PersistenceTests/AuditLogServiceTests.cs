using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class AuditLogServiceTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task GetLastEventTimestampAsync_returns_the_most_recent_CreatedAt_for_that_event_type_only()
    {
        await fixture.ResetAsync();
        var now = DateTimeOffset.UtcNow;
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        dbContext.ClaimAuditLogEntries.Add(new ClaimAuditLog
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            EventType = "SLA_BREACH_DETECTED",
            Description = "first breach",
            CreatedAt = now.AddHours(-30),
        });
        dbContext.ClaimAuditLogEntries.Add(new ClaimAuditLog
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            EventType = "SLA_BREACH_DETECTED",
            Description = "second, more recent breach",
            CreatedAt = now.AddHours(-10),
        });
        dbContext.ClaimAuditLogEntries.Add(new ClaimAuditLog
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            EventType = "CLAIM_CREATED",
            Description = "unrelated event type, more recent than both breaches",
            CreatedAt = now,
        });
        await dbContext.SaveChangesAsync();

        var service = new AuditLogService(dbContext, new NullCurrentUserService());
        var result = await service.GetLastEventTimestampAsync(claimId, "SLA_BREACH_DETECTED");

        Assert.NotNull(result);
        Assert.Equal(now.AddHours(-10), result.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetLastEventTimestampAsync_returns_null_when_no_matching_entries_exist()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        var service = new AuditLogService(dbContext, new NullCurrentUserService());
        var result = await service.GetLastEventTimestampAsync(claimId, "SLA_BREACH_DETECTED");

        Assert.Null(result);
    }

    private async Task<Guid> SeedClaimAsync()
    {
        await using var dbContext = CreateDbContext();
        var claim = new Claim
        {
            OrganisationId = OrganisationId,
            ClaimNumber = $"CLM-2026-{Random.Shared.Next(1, 9999999):D7}",
            Status = ClaimStatus.Open,
            ReportedDate = DateTimeOffset.UtcNow,
        };
        dbContext.Claims.Add(claim);
        await dbContext.SaveChangesAsync();
        return claim.ClaimId;
    }

    private ClaimsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options);

    private sealed class NullCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Role => null;
    }
}
