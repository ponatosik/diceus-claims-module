using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

public class ClaimDocumentRepository(ClaimsDbContext dbContext, IUnitOfWork unitOfWork) : IClaimDocumentRepository
{
    public void Add(ClaimDocument document) => dbContext.ClaimDocuments.Add(document);

    public Task CommitChangesAsync(CancellationToken cancellationToken = default) =>
        unitOfWork.CommitChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<ClaimDocument>> ListByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        await dbContext.ClaimDocuments.Where(d => d.ClaimId == claimId).ToListAsync(cancellationToken);
}
