using ClaimsModule.Domain;

namespace ClaimsModule.Application.Common;

/// <summary>
/// The sole write path to ClaimAuditLog (AUD-02) — no handler ever writes to the audit table via
/// DbContext directly.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Adds the entry to the current change tracker without saving — callers commit it together with
    /// whatever else they're persisting in the same request, keeping the business write and its audit
    /// trail atomic in one SaveChangesAsync call.
    /// </summary>
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ClaimAuditLog> Items, int TotalCount)> GetForClaimAsync(
        Guid claimId, int page, int pageSize, CancellationToken cancellationToken = default);
}
