using System.Text.Json;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.ApproveReserve;

public class ApproveReserveCommandHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    IUserDirectory userDirectory) : IRequestHandler<ApproveReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(ApproveReserveCommand request, CancellationToken cancellationToken)
    {
        var history = await reserveComponentRepository.GetHistoryByIdAsync(request.ReserveHistoryId, cancellationToken);
        if (history is null || history.ClaimId != request.ClaimId)
        {
            throw new NotFoundException("ReserveHistory", request.ReserveHistoryId);
        }

        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);
        ReserveClaimStatusGuard.EnsureMutable(claim);

        if (history.ApprovalStatus != ReserveApprovalStatus.PendingApproval)
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("RESERVE-NOT-PENDING-01", "Only a reserve transaction in PendingApproval status can be approved.")]);
        }

        if (currentUserService.UserId is null || currentUserService.UserId == history.SubmittedByUserId)
        {
            throw new BusinessRuleViolationException([new BlockingCondition("BR-R-03", "Self-approval is not permitted.")]);
        }

        var tier = ReserveAuthority.GetTier(history.Amount);
        if (!ReserveAuthority.CanApprove(tier, currentUserService.Role))
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("BR-R-02", "Your role does not have authority to approve this reserve amount.")]);
        }

        var currentTotal = await reserveComponentRepository.GetTotalCurrentAmountByClaimIdAsync(request.ClaimId, cancellationToken);
        var limitConditions = ReserveAggregateLimit.Evaluate(currentTotal, history.Amount, claim.ReserveOverrideApproved);
        if (limitConditions.Count > 0)
        {
            throw new BusinessRuleViolationException(limitConditions);
        }

        var component = await reserveComponentRepository.GetByIdAsync(history.ReserveComponentId, cancellationToken)
            ?? throw new NotFoundException("ClaimReserveComponent", history.ReserveComponentId);

        // Re-read live rather than trust what was snapshotted at submission time (Resolved Ambiguity 5).
        var previousBalance = component.CurrentAmount;
        var newBalance = previousBalance + history.Amount;
        history.PreviousBalance = previousBalance;
        history.NewBalance = newBalance;
        component.CurrentAmount = newBalance;

        history.ApprovalStatus = ReserveApprovalStatus.Approved;
        history.ApprovedByUserId = currentUserService.UserId;
        history.ApprovedAt = DateTimeOffset.UtcNow;

        reserveComponentRepository.AddOutboxMessage(new OutboxMessage
        {
            OutboxMessageId = Guid.NewGuid(),
            Type = "EnqueueGLPostingJob",
            Payload = JsonSerializer.Serialize(new { history.ReserveHistoryId, history.IdempotencyKey }),
        });

        await auditLogService.LogAsync(
            new AuditLogEntry(
                claim.ClaimId, "RESERVE_APPROVED", $"Reserve approved: {component.Component} ({history.Amount:C}).",
                null, null, history.ReserveHistoryId, "ReserveHistory"),
            cancellationToken);

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);

        var displayNames = await userDirectory.GetDisplayNamesAsync(ReserveTransactionMapper.UserIdsOf(history), cancellationToken);
        return ReserveTransactionMapper.ToDto(history, component.Component, displayNames);
    }
}
