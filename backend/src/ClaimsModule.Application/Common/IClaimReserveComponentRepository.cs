using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common;

/// <summary>Repository for the ClaimReserveComponent aggregate root (ADR-0003). Query members are added as handlers need them.</summary>
public interface IClaimReserveComponentRepository
{
    void Add(ClaimReserveComponent component);
    Task CommitChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimReserveComponent>> ListByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>CC-01: whether any ReserveHistory transaction for this claim is still awaiting approval.</summary>
    Task<bool> AnyReserveHistoryPendingApprovalAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>Pending Amount (GLOSSARY.md) per component: sum of each component's PendingApproval transactions. Components with none are absent.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetPendingAmountsByComponentAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>Open/UnderInvestigation→PendingPayment's condition: at least one approved reserve transaction.</summary>
    Task<bool> AnyReserveHistoryApprovedOrAutoApprovedAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>Sum of CurrentAmount across all reserve components, grouped by ClaimId — for ListClaimsQuery's totalReserves field.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetTotalReservesByClaimIdsAsync(IEnumerable<Guid> claimIds, CancellationToken cancellationToken = default);

    Task<ClaimReserveComponent?> GetByIdAsync(Guid reserveComponentId, CancellationToken cancellationToken = default);

    /// <summary>The "open-or-adjust" lookup — decides whether OpenReserveCommand creates a new component or reuses an existing one.</summary>
    Task<ClaimReserveComponent?> GetByClaimIdAndComponentAsync(Guid claimId, ReserveComponentType component, CancellationToken cancellationToken = default);

    Task<ReserveHistory?> GetHistoryByIdAsync(Guid reserveHistoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A fresh, untracked read of just this one column — used to distinguish "another writer already
    /// posted this" (a genuine race) from any other concurrency conflict after a failed save, without
    /// returning the same stale tracked instance the failed save already has in the change tracker.
    /// </summary>
    Task<ReservePostingStatus?> GetHistoryPostingStatusAsync(Guid reserveHistoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReserveHistory>> ListHistoryByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default);

    /// <summary>1 + the count of existing ReserveHistory rows for this component (or 1 if none exist yet) — ADR-0005's per-component ChangeSequence.</summary>
    Task<int> GetNextChangeSequenceAsync(Guid reserveComponentId, CancellationToken cancellationToken = default);

    /// <summary>Single-claim variant of GetTotalReservesByClaimIdsAsync, used by the BR-R-05 $10M aggregate check.</summary>
    Task<decimal> GetTotalCurrentAmountByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default);

    void AddHistory(ReserveHistory history);

    /// <summary>Added to the same tracked graph as the reserve approval that produced it (ADR-0004) — not a separate repository/commit.</summary>
    void AddOutboxMessage(OutboxMessage message);
}
