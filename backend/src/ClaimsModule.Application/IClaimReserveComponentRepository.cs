using ClaimsModule.Domain;

namespace ClaimsModule.Application;

/// <summary>Repository for the ClaimReserveComponent aggregate root (ADR-0003). Query members are added as handlers need them.</summary>
public interface IClaimReserveComponentRepository
{
    void Add(ClaimReserveComponent component);
    Task CommitChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimReserveComponent>> ListByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>CC-01: whether any ReserveHistory transaction for this claim is still awaiting approval.</summary>
    Task<bool> AnyReserveHistoryPendingApprovalAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>Open/UnderInvestigation→PendingPayment's condition: at least one approved reserve transaction.</summary>
    Task<bool> AnyReserveHistoryApprovedOrAutoApprovedAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>Sum of CurrentAmount across all reserve components, grouped by ClaimId — for ListClaimsQuery's totalReserves field.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetTotalReservesByClaimIdsAsync(IEnumerable<Guid> claimIds, CancellationToken cancellationToken = default);
}
