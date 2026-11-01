using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// The claim status state machine from REQUIREMENTS.md §4.2, encoded as static application logic
/// per Slice A's decision not to persist a ClaimStatusTransitions table. Any transition not listed
/// here is invalid (BR-ST-01).
/// </summary>
public static class ClaimStatusTransitions
{
    private static readonly Dictionary<ClaimStatus, ClaimStatus[]> ValidNextStatuses = new()
    {
        [ClaimStatus.Draft] = [ClaimStatus.Open],
        [ClaimStatus.Open] = [ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, ClaimStatus.Closed, ClaimStatus.Withdrawn],
        [ClaimStatus.UnderInvestigation] = [ClaimStatus.Open, ClaimStatus.PendingPayment, ClaimStatus.Closed, ClaimStatus.Withdrawn],
        [ClaimStatus.PendingPayment] = [ClaimStatus.Closed],
        [ClaimStatus.Closed] = [ClaimStatus.Reopened],
        // Never durably observed as a persisted status — TransitionClaimStatusCommandHandler cascades
        // Closed->Reopened straight through to Open in one call (see the plan's Resolved Ambiguity 5).
        // Still listed here because it documents the state machine faithfully for ListClaimStatusesQuery.
        [ClaimStatus.Reopened] = [ClaimStatus.Open],
        [ClaimStatus.Withdrawn] = [],
    };

    public static IReadOnlyList<ClaimStatus> GetValidNextStatuses(ClaimStatus from) =>
        ValidNextStatuses.TryGetValue(from, out var next) ? next : [];

    public static bool IsValidTransition(ClaimStatus from, ClaimStatus to) =>
        GetValidNextStatuses(from).Contains(to);
}
