using ClaimsModule.Domain;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class ReserveTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task Decimal_amounts_round_trip_at_19_4_precision_without_truncation()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        var component = new ClaimReserveComponent
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            Component = ReserveComponentType.Indemnity,
            CurrentAmount = 12345.6789m,
            Status = ReserveComponentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ClaimReserveComponents.Add(component);
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.ClaimReserveComponents.SingleAsync(c => c.ReserveComponentId == component.ReserveComponentId);

        Assert.Equal(12345.6789m, reread.CurrentAmount);
        Assert.NotNull(reread.RowVer);
        Assert.NotEmpty(reread.RowVer);
    }

    [Fact]
    public async Task A_reserve_history_row_records_the_sequential_delta_fields()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        var component = new ClaimReserveComponent
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            Component = ReserveComponentType.Expense,
            CurrentAmount = 5000m,
            Status = ReserveComponentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ClaimReserveComponents.Add(component);
        await dbContext.SaveChangesAsync();

        var history = new ReserveHistory
        {
            OrganisationId = OrganisationId,
            ReserveComponentId = component.ReserveComponentId,
            ClaimId = claimId,
            TransactionType = ReserveTransactionType.Add,
            Amount = 5000m,
            PreviousBalance = 0m,
            NewBalance = 5000m,
            ApprovalStatus = ReserveApprovalStatus.AutoApproved,
            ChangeReason = "Initial reserve",
            PostingStatus = ReservePostingStatus.Pending,
            IdempotencyKey = $"Reserve:{component.ReserveComponentId}:Change:1",
            ChangeSequence = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ReserveHistoryEntries.Add(history);
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.ReserveHistoryEntries.SingleAsync(h => h.ReserveHistoryId == history.ReserveHistoryId);

        Assert.Equal(0m, reread.PreviousBalance);
        Assert.Equal(5000m, reread.NewBalance);
        Assert.Equal(1, reread.ChangeSequence);
    }

    [Fact]
    public async Task IdempotencyKey_has_a_database_level_unique_constraint()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        var component = new ClaimReserveComponent
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            Component = ReserveComponentType.Indemnity,
            CurrentAmount = 1000m,
            Status = ReserveComponentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ClaimReserveComponents.Add(component);
        await dbContext.SaveChangesAsync();

        const string duplicateKey = "Reserve:duplicate-test:Change:1";
        dbContext.ReserveHistoryEntries.Add(new ReserveHistory
        {
            OrganisationId = OrganisationId,
            ReserveComponentId = component.ReserveComponentId,
            ClaimId = claimId,
            TransactionType = ReserveTransactionType.Add,
            Amount = 1000m,
            PreviousBalance = 0m,
            NewBalance = 1000m,
            ApprovalStatus = ReserveApprovalStatus.AutoApproved,
            ChangeReason = "First",
            PostingStatus = ReservePostingStatus.Posted,
            IdempotencyKey = duplicateKey,
            ChangeSequence = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        dbContext.ReserveHistoryEntries.Add(new ReserveHistory
        {
            OrganisationId = OrganisationId,
            ReserveComponentId = component.ReserveComponentId,
            ClaimId = claimId,
            TransactionType = ReserveTransactionType.Add,
            Amount = 500m,
            PreviousBalance = 1000m,
            NewBalance = 1500m,
            ApprovalStatus = ReserveApprovalStatus.AutoApproved,
            ChangeReason = "Duplicate idempotency key — should be rejected",
            PostingStatus = ReservePostingStatus.Pending,
            IdempotencyKey = duplicateKey,
            ChangeSequence = 2,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
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
}
