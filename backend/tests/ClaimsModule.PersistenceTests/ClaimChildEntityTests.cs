using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class ClaimChildEntityTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task A_loss_event_round_trips_linked_to_its_claim()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        dbContext.LossEvents.Add(new LossEvent
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            LossDate = DateTimeOffset.UtcNow.AddDays(-2),
            LossDescription = "Vehicle collided with a parked car in the lot.",
            LossLocation = "Main St parking lot",
            CauseOfLossCode = "COL-VEH-COL",
            EstimatedLossAmount = 4500.00m,
            ReportDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.LossEvents.SingleAsync(e => e.ClaimId == claimId);
        Assert.Equal("COL-VEH-COL", reread.CauseOfLossCode);
        Assert.Equal(4500.00m, reread.EstimatedLossAmount);
    }

    [Fact]
    public async Task A_claim_party_round_trips_with_its_role_and_type()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        dbContext.ClaimParties.Add(new ClaimParty
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            PartyRole = PartyRole.Claimant,
            PartyType = PartyType.Person,
            FirstName = "Jane",
            LastName = "Doe",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.ClaimParties.SingleAsync(p => p.ClaimId == claimId);
        Assert.Equal(PartyRole.Claimant, reread.PartyRole);
        Assert.Equal(PartyType.Person, reread.PartyType);
        Assert.True(reread.IsActive);
    }

    [Fact]
    public async Task A_risk_object_round_trips_with_its_asset_type()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        dbContext.ClaimRiskObjects.Add(new ClaimRiskObject
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            AssetType = AssetType.Vehicle,
            AssetDescription = "2019 Ford Transit cargo van",
            IsPrimary = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.ClaimRiskObjects.SingleAsync(r => r.ClaimId == claimId);
        Assert.Equal(AssetType.Vehicle, reread.AssetType);
        Assert.True(reread.IsPrimary);
    }

    [Fact]
    public async Task A_claim_document_round_trips_with_blob_metadata()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        dbContext.ClaimDocuments.Add(new ClaimDocument
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            DocumentType = "PoliceReport",
            DocumentName = "police-report.pdf",
            BlobPath = $"claim-documents/{OrganisationId}/{claimId}/police-report.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 204800,
            UploadedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.ClaimDocuments.SingleAsync(d => d.ClaimId == claimId);
        Assert.Equal(204800, reread.FileSizeBytes);
        Assert.StartsWith("claim-documents/", reread.BlobPath);
    }

    [Fact]
    public async Task An_audit_log_entry_round_trips()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        dbContext.ClaimAuditLogEntries.Add(new ClaimAuditLog
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            EventType = "CLAIM_CREATED",
            Description = "Claim created via FNOL intake.",
            CorrelationId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.ClaimAuditLogEntries.SingleAsync(a => a.ClaimId == claimId);
        Assert.Equal("CLAIM_CREATED", reread.EventType);
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
