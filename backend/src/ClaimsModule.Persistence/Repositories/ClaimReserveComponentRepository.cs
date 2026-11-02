using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

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

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetPendingAmountsByComponentAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        await dbContext.ReserveHistoryEntries
            .Where(h => h.ClaimId == claimId && h.ApprovalStatus == ReserveApprovalStatus.PendingApproval)
            .GroupBy(h => h.ReserveComponentId)
            .Select(g => new { ReserveComponentId = g.Key, PendingAmount = g.Sum(h => h.Amount) })
            .ToDictionaryAsync(x => x.ReserveComponentId, x => x.PendingAmount, cancellationToken);

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

    public Task<ClaimReserveComponent?> GetByIdAsync(Guid reserveComponentId, CancellationToken cancellationToken = default) =>
        dbContext.ClaimReserveComponents.FirstOrDefaultAsync(c => c.ReserveComponentId == reserveComponentId, cancellationToken);

    public Task<ClaimReserveComponent?> GetByClaimIdAndComponentAsync(
        Guid claimId, ReserveComponentType component, CancellationToken cancellationToken = default) =>
        dbContext.ClaimReserveComponents.FirstOrDefaultAsync(c => c.ClaimId == claimId && c.Component == component, cancellationToken);

    public Task<ReserveHistory?> GetHistoryByIdAsync(Guid reserveHistoryId, CancellationToken cancellationToken = default) =>
        dbContext.ReserveHistoryEntries.FirstOrDefaultAsync(h => h.ReserveHistoryId == reserveHistoryId, cancellationToken);

    public Task<ReservePostingStatus?> GetHistoryPostingStatusAsync(Guid reserveHistoryId, CancellationToken cancellationToken = default) =>
        dbContext.ReserveHistoryEntries
            .AsNoTracking()
            .Where(h => h.ReserveHistoryId == reserveHistoryId)
            .Select(h => (ReservePostingStatus?)h.PostingStatus)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReserveHistory>> ListHistoryByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        await dbContext.ReserveHistoryEntries
            .Where(h => h.ClaimId == claimId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextChangeSequenceAsync(Guid reserveComponentId, CancellationToken cancellationToken = default) =>
        1 + await dbContext.ReserveHistoryEntries.CountAsync(h => h.ReserveComponentId == reserveComponentId, cancellationToken);

    public Task<decimal> GetTotalCurrentAmountByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        dbContext.ClaimReserveComponents.Where(c => c.ClaimId == claimId).SumAsync(c => c.CurrentAmount, cancellationToken);

    public void AddHistory(ReserveHistory history) => dbContext.ReserveHistoryEntries.Add(history);

    public void AddOutboxMessage(OutboxMessage message) => dbContext.OutboxMessages.Add(message);
}
