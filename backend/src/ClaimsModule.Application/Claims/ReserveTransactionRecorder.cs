using System.Text.Json;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// The one place a reserve transaction is recorded (§6, BR-R-*): component open-or-reuse, Authority
/// Tier routing, the BR-R-05 aggregate check on auto-approval, ADR-0005's idempotency key, the GL
/// outbox message (ADR-0004) and the audit entries. Shared by <c>OpenReserveCommandHandler</c> and
/// FNOL's optional initial reserve (<c>CreateClaimCommandHandler</c>) so the two can never diverge.
/// Adds to the tracked graph only — the caller commits, so FNOL stays a single transaction.
/// </summary>
public class ReserveTransactionRecorder(
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService)
{
    public async Task<(ReserveHistory History, ClaimReserveComponent Component)> RecordAsync(
        Claim claim,
        ReserveComponentType componentType,
        decimal amount,
        string changeReason,
        ReserveTransactionType transactionType,
        CancellationToken cancellationToken)
    {
        var component = await reserveComponentRepository.GetByClaimIdAndComponentAsync(claim.ClaimId, componentType, cancellationToken);
        ReserveBalanceRules.EnsureSatisfied(componentType, transactionType, component is not null, component?.CurrentAmount ?? 0m, amount);
        if (component is null)
        {
            // Client-side id assignment — the history row below needs a real ReserveComponentId in
            // the same SaveChanges call. The Claim navigation (not just ClaimId) lets EF order the
            // inserts when the claim itself is new in this same transaction (FNOL).
            component = new ClaimReserveComponent
            {
                ReserveComponentId = Guid.NewGuid(),
                ClaimId = claim.ClaimId,
                Claim = claim,
                Component = componentType,
                CurrentAmount = 0m,
                Status = ReserveComponentStatus.Active,
            };
            reserveComponentRepository.Add(component);
        }

        var previousBalance = component.CurrentAmount;
        var newBalance = previousBalance + amount;
        var tier = ReserveAuthority.GetTier(amount);
        var autoApproved = tier == ReserveAuthorityTier.Auto;

        if (autoApproved)
        {
            var currentTotal = await reserveComponentRepository.GetTotalCurrentAmountByClaimIdAsync(claim.ClaimId, cancellationToken);
            var limitConditions = ReserveAggregateLimit.Evaluate(currentTotal, amount, claim.ReserveOverrideApproved);
            if (limitConditions.Count > 0)
            {
                throw new BusinessRuleViolationException(limitConditions);
            }
        }

        var sequence = await reserveComponentRepository.GetNextChangeSequenceAsync(component.ReserveComponentId, cancellationToken);

        var history = new ReserveHistory
        {
            ReserveHistoryId = Guid.NewGuid(),
            ClaimId = claim.ClaimId,
            ReserveComponentId = component.ReserveComponentId,
            TransactionType = transactionType,
            Amount = amount,
            PreviousBalance = previousBalance,
            NewBalance = newBalance,
            ChangeReason = changeReason,
            SubmittedByUserId = currentUserService.UserId,
            ChangeSequence = sequence,
            IdempotencyKey = $"Reserve:{component.ReserveComponentId}:Change:{sequence}",
            PostingStatus = ReservePostingStatus.Pending,
            ApprovalStatus = autoApproved ? ReserveApprovalStatus.AutoApproved : ReserveApprovalStatus.PendingApproval,
        };
        reserveComponentRepository.AddHistory(history);

        if (autoApproved)
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
                claim.ClaimId, "RESERVE_CREATED", $"Reserve transaction submitted for {componentType} ({AuditMoney.Format(amount)}).",
                null, null, history.ReserveHistoryId, "ReserveHistory"),
            cancellationToken);

        if (autoApproved)
        {
            await auditLogService.LogAsync(
                new AuditLogEntry(
                    claim.ClaimId, "RESERVE_AUTO_APPROVED", $"Reserve auto-approved: {componentType} ({AuditMoney.Format(amount)}).",
                    null, null, history.ReserveHistoryId, "ReserveHistory"),
                cancellationToken);
        }

        return (history, component);
    }
}
