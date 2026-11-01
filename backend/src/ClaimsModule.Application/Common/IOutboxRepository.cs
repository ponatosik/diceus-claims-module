using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common;

/// <summary>
/// Read side of the outbox pattern (ADR-0004) — the dispatcher's repository. Writing an
/// <see cref="OutboxMessage"/> stays on <see cref="IClaimReserveComponentRepository.AddOutboxMessage"/>,
/// since that write must land in the same transaction as the business change that caused it; this
/// repository is only for the separate, later transaction that reads and dispatches unprocessed rows.
/// </summary>
public interface IOutboxRepository
{
    Task CommitChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> ListUnprocessedAsync(CancellationToken cancellationToken = default);
}
