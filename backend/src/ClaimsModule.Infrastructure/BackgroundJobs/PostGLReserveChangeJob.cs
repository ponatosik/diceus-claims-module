using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Simulates a GL posting for an approved reserve transaction (§6.5/§12.1). Idempotent two ways
/// (ADR-0005): a primary check-then-write against <see cref="ReserveHistory.PostingStatus"/>, and a
/// race backstop that treats a losing concurrent execution as already-processed rather than a failure
/// (see the catch below).
/// </summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 5, 15, 30 })]
public class PostGLReserveChangeJob(
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService)
{
    public async Task ExecuteAsync(Guid reserveHistoryId, string idempotencyKey, string postingJobId)
    {
        var history = await reserveComponentRepository.GetHistoryByIdAsync(reserveHistoryId);
        if (history is null || history.PostingStatus == ReservePostingStatus.Posted)
        {
            return; // §12.1's primary idempotency check — already processed, nothing to do.
        }

        var component = await reserveComponentRepository.GetByIdAsync(history.ReserveComponentId)
            ?? throw new InvalidOperationException($"ClaimReserveComponent {history.ReserveComponentId} not found for ReserveHistory {reserveHistoryId}.");

        history.PostingStatus = ReservePostingStatus.Posted;
        history.PostingJobId = postingJobId;

        using var activity = BackgroundJobActivitySource.Source.StartActivity(nameof(PostGLReserveChangeJob));
        await auditLogService.LogAsync(new AuditLogEntry(
            history.ClaimId, "GL_POSTING_SIMULATED",
            $"DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = {history.Amount:C}.",
            null, null, history.ReserveHistoryId, "ReserveHistory"));

        try
        {
            await reserveComponentRepository.CommitChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // ADR-0005's race backstop: a second near-simultaneous execution lost the race on
            // ReserveHistory's RowVer concurrency token (added in Slice C's review fix pass). ADR-0005
            // itself describes this backstop as a unique-constraint violation on IdempotencyKey,
            // written before that token existed; RowVer now serves the identical purpose — the first
            // writer to reach PostingStatus=Posted wins, the second's update of the same row is
            // rejected — without a redundant marker table. Treat as already-processed, not a genuine
            // failure: never let Hangfire retry a race that already succeeded elsewhere.
        }
    }
}
