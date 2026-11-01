using System.Diagnostics;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class AuditLogService(ClaimsDbContext dbContext, ICurrentUserService currentUserService) : IAuditLogService
{
    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.ClaimAuditLogEntries.Add(new ClaimAuditLog
        {
            ClaimId = entry.ClaimId,
            EventType = entry.EventType,
            Description = entry.Description,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            RelatedEntityId = entry.RelatedEntityId,
            RelatedEntityType = entry.RelatedEntityType,
            CorrelationId = GetCorrelationId(),
            CreatedByUserId = currentUserService.UserId,
        });

        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<ClaimAuditLog> Items, int TotalCount)> GetForClaimAsync(
        Guid claimId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ClaimAuditLogEntries.Where(a => a.ClaimId == claimId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>Sourced from Activity.Current's trace id, per wayfinder ticket 8 — not a bespoke header.</summary>
    private static Guid? GetCorrelationId()
    {
        var traceId = Activity.Current?.TraceId.ToHexString();
        return traceId is not null && Guid.TryParse(traceId, out var correlationId) ? correlationId : null;
    }
}
