namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Bridges a same-database transaction (e.g. reserve approval) into an eventual out-of-process side
/// effect (e.g. enqueueing a Hangfire job in its own separate store) without a distributed transaction
/// (ADR-0004). Written in the same transaction as the event that caused it; a separate dispatcher
/// (Slice D) reads unprocessed rows and marks them processed once handled.
/// </summary>
public class OutboxMessage : AppendOnlyEntity
{
    public Guid OutboxMessageId { get; set; }

    /// <summary>Discriminates the kind of message (e.g. "EnqueueGLPostingJob").</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON payload for the dispatcher to act on.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset? ProcessedAt { get; set; }
}
