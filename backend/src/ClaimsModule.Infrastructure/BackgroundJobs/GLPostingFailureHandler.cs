using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Business logic for §12.1's failure path — "after all retries exhausted: set PostingStatus =
/// Failed, write GL_POSTING_FAILED". Called by <see cref="GLPostingFailureFilter"/> once Hangfire
/// gives up retrying <see cref="PostGLReserveChangeJob"/>, and directly by tests.
/// </summary>
public class GLPostingFailureHandler(
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService)
{
    public async Task RecordFinalFailureAsync(Guid reserveHistoryId, string failureReason, CancellationToken cancellationToken = default)
    {
        var history = await reserveComponentRepository.GetHistoryByIdAsync(reserveHistoryId, cancellationToken);
        if (history is null || history.PostingStatus == ReservePostingStatus.Posted)
        {
            return; // already succeeded elsewhere — don't overwrite a real success with a stale failure signal.
        }

        history.PostingStatus = ReservePostingStatus.Failed;

        using var activity = BackgroundJobActivitySource.Source.StartActivity(nameof(GLPostingFailureHandler));
        await auditLogService.LogAsync(new AuditLogEntry(
            history.ClaimId, "GL_POSTING_FAILED", $"GL posting failed after exhausting retries: {failureReason}",
            null, failureReason, history.ReserveHistoryId, "ReserveHistory"), cancellationToken);

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);
    }
}
