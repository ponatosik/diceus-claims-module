using ClaimsModule.Application;
using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ClaimDocumentRepository(ClaimsDbContext dbContext) : IClaimDocumentRepository
{
    public async Task<IReadOnlyList<ClaimDocument>> ListByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        await dbContext.ClaimDocuments.Where(d => d.ClaimId == claimId).ToListAsync(cancellationToken);
}
