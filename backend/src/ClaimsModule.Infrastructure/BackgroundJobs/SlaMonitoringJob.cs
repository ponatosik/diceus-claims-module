using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job (§12.2): flags claims stuck in Draft/Open for 48+ hours by writing an audit-only
/// SLA_BREACH_DETECTED entry — never changes claim status, never sends notifications.
/// </summary>
public class SlaMonitoringJob(IClaimRepository claimRepository, IAuditLogService auditLogService)
{
    private static readonly ClaimStatus[] MonitoredStatuses = [ClaimStatus.Draft, ClaimStatus.Open];

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var staleClaims = await claimRepository.ListStaleAsync(MonitoredStatuses, now.AddHours(-48), cancellationToken);

        using var activity = BackgroundJobActivitySource.Source.StartActivity(nameof(SlaMonitoringJob));
        foreach (var claim in staleClaims)
        {
            var lastBreach = await auditLogService.GetLastEventTimestampAsync(claim.ClaimId, "SLA_BREACH_DETECTED", cancellationToken);
            if (lastBreach is not null && lastBreach.Value >= now.AddHours(-24))
            {
                continue; // §12.2's dedupe rule: skip if a breach was already recorded within 24h.
            }

            await auditLogService.LogAsync(
                new AuditLogEntry(claim.ClaimId, "SLA_BREACH_DETECTED", "Claim has not been updated in 48 hours."),
                cancellationToken);
        }

        await claimRepository.CommitChangesAsync(cancellationToken);
    }
}
