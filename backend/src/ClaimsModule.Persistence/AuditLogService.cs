using System.Diagnostics;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class AuditLogService(ClaimsDbContext dbContext, ICurrentUserService currentUserService) : IAuditLogService
{
    // Root cause of an intermittent StatusTransitionTests flake: the SaveChanges interceptor used to
    // call DateTimeOffset.UtcNow fresh per entity, and the Reopen cascade's three LogAsync calls land
    // in one SaveChanges — on coarser clock resolutions two calls in that tight loop can return the
    // same instant, making CreatedAt ties (and therefore audit-log order) nondeterministic. Rather
    // than hope a secondary sort key (e.g. the NEWSEQUENTIALID() AuditLogId) happens to track true
    // call order — it isn't a documented guarantee, and this was the fix that didn't hold up — this
    // service assigns a strictly-monotonic CreatedAt itself, scoped to one request's AuditLogService
    // instance, so LogAsync call order and CreatedAt order are the same thing by construction.
    private DateTimeOffset _lastCreatedAt = DateTimeOffset.MinValue;

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
            CreatedAt = NextCreatedAt(),
        });

        return Task.CompletedTask;
    }

    private DateTimeOffset NextCreatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        _lastCreatedAt = now > _lastCreatedAt ? now : _lastCreatedAt.AddTicks(1);
        return _lastCreatedAt;
    }

    public async Task<(IReadOnlyList<ClaimAuditLog> Items, int TotalCount)> GetForClaimAsync(
        Guid claimId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ClaimAuditLogEntries.Where(a => a.ClaimId == claimId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            // CreatedAt is now strictly monotonic per request (see NextCreatedAt() above), so ties
            // within one request shouldn't occur. This ThenBy is a last-resort tiebreak only — across
            // different requests/AuditLogService instances, AuditLogId's NEWSEQUENTIALID() generation
            // order is NOT a documented guarantee of true insert order.
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AuditLogId)
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
