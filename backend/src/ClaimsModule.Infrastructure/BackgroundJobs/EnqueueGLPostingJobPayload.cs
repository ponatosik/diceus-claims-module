namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Matches the payload shape <c>OpenReserveCommandHandler</c>/<c>ApproveReserveCommandHandler</c>
/// (Slice C) serialise onto an <c>OutboxMessage</c> of Type <c>"EnqueueGLPostingJob"</c>.
/// </summary>
public record EnqueueGLPostingJobPayload(Guid ReserveHistoryId, string IdempotencyKey);
