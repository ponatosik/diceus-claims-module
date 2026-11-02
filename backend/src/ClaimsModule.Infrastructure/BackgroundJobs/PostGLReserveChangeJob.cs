using System.Globalization;
using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
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
    public async Task ExecuteAsync(Guid reserveHistoryId, string postingJobId)
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

        // Pinned to en-US, not the host's current culture — a Hangfire worker runs under whatever
        // culture was ambient when the job was enqueued (its CaptureCultureAttribute default), which
        // must never leak into a permanent audit record's formatting. InvariantCulture's own "C"
        // format has no actual currency symbol (renders "¤"), so en-US is pinned explicitly instead.
        var journalDetail =
            $"DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = {AuditMoney.Format(history.Amount)}.";

        using var activity = BackgroundJobActivitySource.Source.StartActivity(nameof(PostGLReserveChangeJob));
        await auditLogService.LogAsync(new AuditLogEntry(
            history.ClaimId, "GL_POSTING_SIMULATED", journalDetail,
            null, journalDetail, history.ReserveHistoryId, "ReserveHistory"));

        try
        {
            await reserveComponentRepository.CommitChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // ADR-0005's race backstop: a second near-simultaneous execution can lose the race on
            // ReserveHistory's RowVer concurrency token (added in Slice C's review fix pass). ADR-0005
            // itself describes this backstop as a unique-constraint violation on IdempotencyKey,
            // written before that token existed; RowVer now serves a similar purpose, but a RowVer
            // conflict fires for ANY concurrent modification of the row, not only "someone else already
            // posted it" — so re-check the actual outcome before deciding this was a benign race rather
            // than a genuine conflict that still needs to be retried.
            var postingStatus = await reserveComponentRepository.GetHistoryPostingStatusAsync(reserveHistoryId);
            if (postingStatus != ReservePostingStatus.Posted)
            {
                throw;
            }
        }
    }
}
