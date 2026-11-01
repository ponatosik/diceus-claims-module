using System.Text.Json;
using ClaimsModule.Application;
using ClaimsModule.Domain;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Bridges Slice C's <see cref="OutboxMessage"/> rows into real Hangfire job enqueues (ADR-0004).
/// Marks each message processed only after a successful enqueue; a crash between the two is safe
/// because <see cref="PostGLReserveChangeJob"/> is itself idempotent on the enqueued arguments.
/// </summary>
public class OutboxDispatcherJob(
    IOutboxRepository outboxRepository, IBackgroundJobClient backgroundJobClient, ILogger<OutboxDispatcherJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var messages = await outboxRepository.ListUnprocessedAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(message);
            }
            catch (Exception exception)
            {
                // One malformed/undeserialisable row must never block every other unprocessed row
                // behind it in the batch, nor the commit of the ones that already succeeded this
                // run — leave it unprocessed (ProcessedAt stays null) so it's visible and retried,
                // rather than silently dropped or stalling the whole dispatcher forever.
                logger.LogError(exception, "Failed to dispatch OutboxMessage {OutboxMessageId} of type {Type}", message.OutboxMessageId, message.Type);
            }
        }

        await outboxRepository.CommitChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Enqueues the Hangfire job for one outbox row and marks it processed, without committing —
    /// callers (normally <see cref="ExecuteAsync"/>) own the commit, so a batch of messages commits
    /// together. Exposed separately so tests can drive the per-message dispatch logic directly.
    /// </summary>
    public Task DispatchAsync(OutboxMessage message)
    {
        switch (message.Type)
        {
            case "EnqueueGLPostingJob":
                var payload = JsonSerializer.Deserialize<EnqueueGLPostingJobPayload>(message.Payload)!;
                var postingJobId = Guid.NewGuid().ToString();
                backgroundJobClient.Enqueue<PostGLReserveChangeJob>(
                    j => j.ExecuteAsync(payload.ReserveHistoryId, payload.IdempotencyKey, postingJobId));
                message.ProcessedAt = DateTimeOffset.UtcNow;
                break;

            default:
                // An outbox message type this dispatcher doesn't (yet) know how to handle — leave it
                // unprocessed rather than silently dropping it; a future message type's dispatcher
                // can pick it up once added.
                break;
        }

        return Task.CompletedTask;
    }
}
