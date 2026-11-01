using ClaimsModule.Application;
using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ClaimReserveComponentRepository(ClaimsDbContext dbContext, IUnitOfWork unitOfWork) : IClaimReserveComponentRepository
{
    public void Add(ClaimReserveComponent component) => dbContext.ClaimReserveComponents.Add(component);

    public async Task CommitChangesAsync(CancellationToken cancellationToken = default) =>
        await unitOfWork.CommitChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<ClaimReserveComponent>> ListByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        await dbContext.ClaimReserveComponents.Where(c => c.ClaimId == claimId).ToListAsync(cancellationToken);

    public Task<bool> AnyReserveHistoryPendingApprovalAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        dbContext.ReserveHistoryEntries.AnyAsync(
            h => h.ClaimId == claimId && h.ApprovalStatus == ReserveApprovalStatus.PendingApproval, cancellationToken);

    public Task<bool> AnyReserveHistoryApprovedOrAutoApprovedAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        dbContext.ReserveHistoryEntries.AnyAsync(
            h => h.ClaimId == claimId && (h.ApprovalStatus == ReserveApprovalStatus.Approved || h.ApprovalStatus == ReserveApprovalStatus.AutoApproved),
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetTotalReservesByClaimIdsAsync(
        IEnumerable<Guid> claimIds, CancellationToken cancellationToken = default)
    {
        var ids = claimIds.ToList();
        return await dbContext.ClaimReserveComponents
            .Where(c => ids.Contains(c.ClaimId))
            .GroupBy(c => c.ClaimId)
            .Select(g => new { ClaimId = g.Key, Total = g.Sum(c => c.CurrentAmount) })
            .ToDictionaryAsync(x => x.ClaimId, x => x.Total, cancellationToken);
    }
}
