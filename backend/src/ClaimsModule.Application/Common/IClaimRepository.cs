using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common;

/// <summary>Filters for <see cref="IClaimRepository.ListAsync"/> — all optional, combined with AND.</summary>
public record ClaimListFilter(
    ClaimStatus? Status = null,
    DateOnly? LossDateFrom = null,
    DateOnly? LossDateTo = null,
    Guid? AssignedHandlerId = null,
    string? CauseOfLossCode = null,
    Guid? PolicyId = null,
    string? Search = null);

/// <summary>Repository for the Claim aggregate root (ADR-0003). Query members are added as handlers need them.</summary>
public interface IClaimRepository
{
    void Add(Claim claim);
    Task CommitChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Tracked (reused by both read queries and mutating commands), with LossEvent/Parties/RiskObjects included.</summary>
    Task<Claim?> GetByIdAsync(Guid claimId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Claim> Items, int TotalCount)> ListAsync(
        ClaimListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// §12.2's SLA monitoring query: claims in one of <paramref name="statuses"/> whose last activity
    /// (UpdatedAt, falling back to CreatedAt for a claim never modified since creation) is older than
    /// <paramref name="updatedBefore"/>.
    /// </summary>
    Task<IReadOnlyList<Claim>> ListStaleAsync(
        IReadOnlyCollection<ClaimStatus> statuses, DateTimeOffset updatedBefore, CancellationToken cancellationToken = default);
}
