using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

public class ClaimRepository(ClaimsDbContext dbContext, IUnitOfWork unitOfWork) : IClaimRepository
{
    public void Add(Claim claim) => dbContext.Claims.Add(claim);

    public void AddParty(ClaimParty party) => dbContext.ClaimParties.Add(party);

    public async Task CommitChangesAsync(CancellationToken cancellationToken = default) =>
        await unitOfWork.CommitChangesAsync(cancellationToken);

    public Task<Claim?> GetByIdAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        dbContext.Claims
            .Include(c => c.LossEvent)
            .Include(c => c.Parties)
            .Include(c => c.RiskObjects)
            .FirstOrDefaultAsync(c => c.ClaimId == claimId, cancellationToken);

    public async Task<(IReadOnlyList<Claim> Items, int TotalCount)> ListAsync(
        ClaimListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Claims.Include(c => c.LossEvent).AsQueryable();

        if (filter.Statuses is { Count: > 0 } statuses)
        {
            query = query.Where(c => statuses.Contains(c.Status));
        }

        if (filter.AssignedHandlerId is { } assignedHandlerId)
        {
            query = query.Where(c => c.AssignedHandlerId == assignedHandlerId);
        }

        if (filter.AssignedHandlerIds is { } assignedHandlerIds)
        {
            query = query.Where(c => c.AssignedHandlerId != null && assignedHandlerIds.Contains(c.AssignedHandlerId.Value));
        }

        if (filter.PolicyId is { } policyId)
        {
            query = query.Where(c => c.PolicyId == policyId);
        }

        if (filter.CauseOfLossCode is { } causeOfLossCode)
        {
            query = query.Where(c => c.LossEvent != null && c.LossEvent.CauseOfLossCode == causeOfLossCode);
        }

        if (filter.LossDateFrom is { } lossDateFrom)
        {
            var from = new DateTimeOffset(lossDateFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(c => c.LossEvent != null && c.LossEvent.LossDate >= from);
        }

        if (filter.LossDateTo is { } lossDateTo)
        {
            var to = new DateTimeOffset(lossDateTo.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(c => c.LossEvent != null && c.LossEvent.LossDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(c => c.ClaimNumber.Contains(filter.Search) || (c.ClientName != null && c.ClientName.Contains(filter.Search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.ReportedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Claim>> ListStaleAsync(
        IReadOnlyCollection<ClaimStatus> statuses, DateTimeOffset updatedBefore, CancellationToken cancellationToken = default) =>
        await dbContext.Claims
            .Where(c => statuses.Contains(c.Status) && (c.UpdatedAt ?? c.CreatedAt) < updatedBefore)
            .ToListAsync(cancellationToken);
}
