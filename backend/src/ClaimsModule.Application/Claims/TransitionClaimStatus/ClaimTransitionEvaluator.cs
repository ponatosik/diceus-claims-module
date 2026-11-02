using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.TransitionClaimStatus;

/// <summary>The facts a transition's Blocking Conditions are computed from, beyond the caller's own inputs.</summary>
public record ClaimTransitionEvaluation(
    bool IsValidTransition,
    IReadOnlyList<BlockingCondition> BlockingConditions,
    bool HasOpenReserves);

/// <summary>
/// The single place a status transition's Blocking Conditions are evaluated (§4.2–4.3), shared by
/// <see cref="TransitionClaimStatusCommandHandler"/> (which throws on any) and
/// <c>GetTransitionPreflightQuery</c> (which reports them) so the Pre-flight Check can never disagree
/// with the real transition. Gathers the claim's facts, then defers every rule to the pure
/// <see cref="ClaimTransitionConditions"/>.
/// </summary>
public class ClaimTransitionEvaluator(
    IClaimReserveComponentRepository reserveComponentRepository,
    ICauseOfLossCodeRepository causeOfLossCodeRepository)
{
    public async Task<ClaimTransitionEvaluation> EvaluateAsync(
        Claim claim, ClaimStatus targetStatus, string? role, string? reason, string? justificationNote, CancellationToken cancellationToken)
    {
        if (!ClaimStatusTransitions.IsValidTransition(claim.Status, targetStatus))
        {
            return new ClaimTransitionEvaluation(false, [new BlockingCondition(
                "BR-ST-01",
                $"Transition from {claim.Status} to {targetStatus} is not permitted. " +
                $"Valid next statuses: {string.Join(", ", ClaimStatusTransitions.GetValidNextStatuses(claim.Status))}.")], false);
        }

        var hasOpenReserves = false;
        IReadOnlyList<BlockingCondition> conditions = [];

        switch (targetStatus)
        {
            case ClaimStatus.Open:
                conditions = ClaimTransitionConditions.EvaluateOpenConditions(
                    HasActiveClaimant(claim), LossDateInFuture(claim), LossDescriptionTooShort(claim),
                    await CauseOfLossCodeInactiveAsync(claim, cancellationToken));
                break;

            case ClaimStatus.PendingPayment:
                conditions = ClaimTransitionConditions.EvaluatePendingPaymentConditions(
                    await reserveComponentRepository.AnyReserveHistoryApprovedOrAutoApprovedAsync(claim.ClaimId, cancellationToken));
                break;

            case ClaimStatus.Closed:
                var reserveComponents = await reserveComponentRepository.ListByClaimIdAsync(claim.ClaimId, cancellationToken);
                hasOpenReserves = reserveComponents.Any(c => c.CurrentAmount != 0);
                conditions = ClaimTransitionConditions.EvaluateClosureConditions(
                    await reserveComponentRepository.AnyReserveHistoryPendingApprovalAsync(claim.ClaimId, cancellationToken),
                    LossDateInFuture(claim), LossDescriptionTooShort(claim),
                    await CauseOfLossCodeInactiveAsync(claim, cancellationToken),
                    HasActiveClaimant(claim), hasOpenReserves, justificationNote);
                break;

            case ClaimStatus.Reopened:
                conditions = ClaimTransitionConditions.EvaluateReopenConditions(role, reason);
                break;

            case ClaimStatus.Withdrawn when string.IsNullOrWhiteSpace(reason):
                conditions = [new BlockingCondition("BR-ST-01", "A withdrawal reason is required.")];
                break;
        }

        return new ClaimTransitionEvaluation(true, conditions, hasOpenReserves);
    }

    private static bool HasActiveClaimant(Claim claim) => claim.Parties.Any(p => p.IsActive && p.PartyRole == PartyRole.Claimant);

    private static bool LossDateInFuture(Claim claim) => claim.LossEvent!.LossDate > DateTimeOffset.UtcNow;

    private static bool LossDescriptionTooShort(Claim claim) => claim.LossEvent!.LossDescription.Length < 20;

    private async Task<bool> CauseOfLossCodeInactiveAsync(Claim claim, CancellationToken cancellationToken)
    {
        var causeOfLossCode = await causeOfLossCodeRepository.GetByCodeAsync(claim.LossEvent!.CauseOfLossCode, cancellationToken);
        return causeOfLossCode is not { IsActive: true };
    }
}
