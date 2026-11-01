using ClaimsModule.Domain;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class ClaimDocumentTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task Add_and_CommitChangesAsync_persists_a_ClaimDocument_row()
    {
        await fixture.ResetAsync();
        var claimId = await SeedClaimAsync();

        await using var dbContext = CreateDbContext();
        var repository = new ClaimDocumentRepository(dbContext, new UnitOfWork(dbContext));
        var document = new ClaimDocument
        {
            OrganisationId = OrganisationId,
            ClaimId = claimId,
            DocumentType = "PoliceReport",
            DocumentName = "report.pdf",
            BlobPath = "claim-documents/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/report.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        repository.Add(document);
        await repository.CommitChangesAsync();

        await using var readContext = CreateDbContext();
        var reread = await readContext.ClaimDocuments.SingleAsync(d => d.ClaimDocumentId == document.ClaimDocumentId);
        Assert.Equal("report.pdf", reread.DocumentName);
        Assert.Equal("PoliceReport", reread.DocumentType);
        Assert.Equal("application/pdf", reread.ContentType);
        Assert.Equal(1024, reread.FileSizeBytes);
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
