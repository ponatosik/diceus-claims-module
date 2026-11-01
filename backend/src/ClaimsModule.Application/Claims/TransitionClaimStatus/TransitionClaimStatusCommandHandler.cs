using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.TransitionClaimStatus;

public class TransitionClaimStatusCommandHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    ICauseOfLossCodeRepository causeOfLossCodeRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    IMapper mapper) : IRequestHandler<TransitionClaimStatusCommand, ClaimDto>
{
    public async Task<ClaimDto> Handle(TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        if (!ClaimStatusTransitions.IsValidTransition(claim.Status, request.TargetStatus))
        {
            throw new BusinessRuleViolationException([new BlockingCondition(
                "BR-ST-01",
                $"Transition from {claim.Status} to {request.TargetStatus} is not permitted. " +
                $"Valid next statuses: {string.Join(", ", ClaimStatusTransitions.GetValidNextStatuses(claim.Status))}.")]);
        }

        switch (request.TargetStatus)
        {
            case ClaimStatus.Open:
                await EnsureOpenConditionsMetAsync(claim, cancellationToken);
                break;

            case ClaimStatus.PendingPayment:
                await EnsurePendingPaymentConditionsMetAsync(claim, cancellationToken);
                break;

            case ClaimStatus.Closed:
                await CloseAsync(claim, request, cancellationToken);
                return mapper.Map<ClaimDto>(claim);

            case ClaimStatus.Reopened:
                await ReopenAsync(claim, request, cancellationToken);
                return mapper.Map<ClaimDto>(claim);

            case ClaimStatus.Withdrawn:
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    throw new BusinessRuleViolationException([new BlockingCondition("BR-ST-01", "A withdrawal reason is required.")]);
                }

                break;
        }

        var oldStatus = claim.Status;
        claim.Status = request.TargetStatus;
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "STATUS_CHANGED", $"Status changed from {oldStatus} to {request.TargetStatus}.", oldStatus.ToString(), request.TargetStatus.ToString()),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);

        return mapper.Map<ClaimDto>(claim);
    }

    private async Task EnsureOpenConditionsMetAsync(Claim claim, CancellationToken cancellationToken)
    {
        var hasActiveClaimant = claim.Parties.Any(p => p.IsActive && p.PartyRole == PartyRole.Claimant);
        var lossDateInFuture = claim.LossEvent!.LossDate > DateTimeOffset.UtcNow;
        var lossDescriptionTooShort = claim.LossEvent.LossDescription.Length < 20;
        var causeOfLossCode = await causeOfLossCodeRepository.GetByCodeAsync(claim.LossEvent.CauseOfLossCode, cancellationToken);
        var causeOfLossCodeInactive = causeOfLossCode is not { IsActive: true };

        var conditions = ClaimTransitionConditions.EvaluateOpenConditions(
            hasActiveClaimant, lossDateInFuture, lossDescriptionTooShort, causeOfLossCodeInactive);
        if (conditions.Count > 0)
        {
            throw new BusinessRuleViolationException(conditions);
        }
    }

    private async Task EnsurePendingPaymentConditionsMetAsync(Claim claim, CancellationToken cancellationToken)
    {
        var anyReserveApprovedOrAutoApproved = await reserveComponentRepository.AnyReserveHistoryApprovedOrAutoApprovedAsync(claim.ClaimId, cancellationToken);

        var conditions = ClaimTransitionConditions.EvaluatePendingPaymentConditions(anyReserveApprovedOrAutoApproved);
        if (conditions.Count > 0)
        {
            throw new BusinessRuleViolationException(conditions);
        }
    }

    private async Task CloseAsync(Claim claim, TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var anyReserveHistoryPendingApproval = await reserveComponentRepository.AnyReserveHistoryPendingApprovalAsync(claim.ClaimId, cancellationToken);
        var lossDateInFuture = claim.LossEvent!.LossDate > DateTimeOffset.UtcNow;
        var lossDescriptionTooShort = claim.LossEvent.LossDescription.Length < 20;
        var causeOfLossCode = await causeOfLossCodeRepository.GetByCodeAsync(claim.LossEvent.CauseOfLossCode, cancellationToken);
        var causeOfLossCodeInactive = causeOfLossCode is not { IsActive: true };
        var hasActiveClaimant = claim.Parties.Any(p => p.IsActive && p.PartyRole == PartyRole.Claimant);
        var reserveComponents = await reserveComponentRepository.ListByClaimIdAsync(claim.ClaimId, cancellationToken);
        var anyReserveComponentNonZero = reserveComponents.Any(c => c.CurrentAmount != 0);

        var conditions = ClaimTransitionConditions.EvaluateClosureConditions(
            anyReserveHistoryPendingApproval, lossDateInFuture, lossDescriptionTooShort,
            causeOfLossCodeInactive, hasActiveClaimant, anyReserveComponentNonZero, request.JustificationNote);
        if (conditions.Count > 0)
        {
            throw new BusinessRuleViolationException(conditions);
        }

        var oldStatus = claim.Status;
        claim.Status = ClaimStatus.Closed;
        claim.ClosedAt = DateTimeOffset.UtcNow;
        claim.ClosureReason = request.Reason;

        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "STATUS_CHANGED", $"Status changed from {oldStatus} to {ClaimStatus.Closed}.", oldStatus.ToString(), ClaimStatus.Closed.ToString()),
            cancellationToken);
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "CLAIM_CLOSED", $"Claim {claim.ClaimNumber} closed.", null, request.Reason),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Closed→Reopened cascades straight through to Open in one call (BR-ST-04: "the claim immediately
    /// transitions to Open after Reopened") — Reopened is never a durably persisted status.
    /// </summary>
    private async Task ReopenAsync(Claim claim, TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var conditions = ClaimTransitionConditions.EvaluateReopenConditions(currentUserService.Role, request.Reason);
        if (conditions.Count > 0)
        {
            throw new BusinessRuleViolationException(conditions);
        }

        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "STATUS_CHANGED", $"Status changed from {ClaimStatus.Closed} to {ClaimStatus.Reopened}.", ClaimStatus.Closed.ToString(), ClaimStatus.Reopened.ToString()),
            cancellationToken);
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "CLAIM_REOPENED", $"Claim {claim.ClaimNumber} reopened.", null, request.Reason),
            cancellationToken);

        claim.Status = ClaimStatus.Open;

        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "STATUS_CHANGED", $"Status changed from {ClaimStatus.Reopened} to {ClaimStatus.Open}.", ClaimStatus.Reopened.ToString(), ClaimStatus.Open.ToString()),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);
    }
}
