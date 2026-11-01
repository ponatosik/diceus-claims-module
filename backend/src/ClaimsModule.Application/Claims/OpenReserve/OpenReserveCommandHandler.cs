using System.Text.Json;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.OpenReserve;

public class OpenReserveCommandHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService) : IRequestHandler<OpenReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(OpenReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var component = await reserveComponentRepository.GetByClaimIdAndComponentAsync(request.ClaimId, request.Component, cancellationToken);
        if (component is null)
        {
            // Client-side id assignment (same rationale as Slice B's Claim.ClaimId) — the history row
            // below needs a real ReserveComponentId in the same SaveChanges call.
            component = new ClaimReserveComponent
            {
                ReserveComponentId = Guid.NewGuid(),
                ClaimId = request.ClaimId,
                Component = request.Component,
                CurrentAmount = 0m,
                Status = ReserveComponentStatus.Active,
            };
            reserveComponentRepository.Add(component);
        }

        var previousBalance = component.CurrentAmount;
        var newBalance = previousBalance + request.Amount;
        var tier = ReserveAuthority.GetTier(request.Amount);

        if (tier == ReserveAuthorityTier.Auto)
        {
            var currentTotal = await reserveComponentRepository.GetTotalCurrentAmountByClaimIdAsync(request.ClaimId, cancellationToken);
            var limitConditions = ReserveAggregateLimit.Evaluate(currentTotal, request.Amount, claim.ReserveOverrideApproved);
            if (limitConditions.Count > 0)
            {
                throw new BusinessRuleViolationException(limitConditions);
            }
        }

        var sequence = await reserveComponentRepository.GetNextChangeSequenceAsync(component.ReserveComponentId, cancellationToken);
        var idempotencyKey = $"Reserve:{component.ReserveComponentId}:Change:{sequence}";

        var history = new ReserveHistory
        {
            ReserveHistoryId = Guid.NewGuid(),
            ClaimId = request.ClaimId,
            ReserveComponentId = component.ReserveComponentId,
            TransactionType = request.TransactionType,
            Amount = request.Amount,
            PreviousBalance = previousBalance,
            NewBalance = newBalance,
            ChangeReason = request.ChangeReason,
            SubmittedByUserId = currentUserService.UserId,
            ChangeSequence = sequence,
            IdempotencyKey = idempotencyKey,
            PostingStatus = ReservePostingStatus.Pending,
            ApprovalStatus = tier == ReserveAuthorityTier.Auto ? ReserveApprovalStatus.AutoApproved : ReserveApprovalStatus.PendingApproval,
        };
        reserveComponentRepository.AddHistory(history);

        if (tier == ReserveAuthorityTier.Auto)
        {
            component.CurrentAmount = newBalance;
            reserveComponentRepository.AddOutboxMessage(new OutboxMessage
            {
                OutboxMessageId = Guid.NewGuid(),
                Type = "EnqueueGLPostingJob",
                Payload = JsonSerializer.Serialize(new { history.ReserveHistoryId, history.IdempotencyKey }),
            });
        }

        await auditLogService.LogAsync(
            new AuditLogEntry(
                claim.ClaimId, "RESERVE_CREATED", $"Reserve transaction submitted for {request.Component} ({request.Amount:C}).",
                null, null, history.ReserveHistoryId, "ReserveHistory"),
            cancellationToken);

        if (tier == ReserveAuthorityTier.Auto)
        {
            await auditLogService.LogAsync(
                new AuditLogEntry(
                    claim.ClaimId, "RESERVE_AUTO_APPROVED", $"Reserve auto-approved: {request.Component} ({request.Amount:C}).",
                    null, null, history.ReserveHistoryId, "ReserveHistory"),
                cancellationToken);
        }

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);

        return ReserveTransactionMapper.ToDto(history, component.Component);
    }
}
