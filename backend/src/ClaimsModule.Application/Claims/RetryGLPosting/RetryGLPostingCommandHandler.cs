using System.Text.Json;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.RetryGLPosting;

/// <summary>
/// Re-enqueues a Failed GL posting through the outbox (ADR-0004) under the transaction's original
/// idempotency key (ADR-0005), so a retry can never double-post. Deliberately not gated by
/// <see cref="ReserveClaimStatusGuard"/>: the reserve change was already approved, and getting it
/// into the GL is reconciliation, not new financial activity — it must stay possible after closure.
/// </summary>
public class RetryGLPostingCommandHandler(
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService,
    IUserDirectory userDirectory) : IRequestHandler<RetryGLPostingCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RetryGLPostingCommand request, CancellationToken cancellationToken)
    {
        var history = await reserveComponentRepository.GetHistoryByIdAsync(request.ReserveHistoryId, cancellationToken);
        if (history is null || history.ClaimId != request.ClaimId)
        {
            throw new NotFoundException("ReserveHistory", request.ReserveHistoryId);
        }

        if (history.PostingStatus != ReservePostingStatus.Failed)
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("GL-RETRY-NOT-FAILED-01", "Only a GL posting with status Failed can be retried.")]);
        }

        var component = await reserveComponentRepository.GetByIdAsync(history.ReserveComponentId, cancellationToken)
            ?? throw new NotFoundException("ClaimReserveComponent", history.ReserveComponentId);

        history.PostingStatus = ReservePostingStatus.Pending;

        reserveComponentRepository.AddOutboxMessage(new OutboxMessage
        {
            OutboxMessageId = Guid.NewGuid(),
            Type = "EnqueueGLPostingJob",
            Payload = JsonSerializer.Serialize(new { history.ReserveHistoryId, history.IdempotencyKey }),
        });

        // Not in §14.1's event list — added so a retry is as traceable as the failure it answers.
        await auditLogService.LogAsync(
            new AuditLogEntry(
                history.ClaimId, "GL_POSTING_RETRY_REQUESTED", $"GL posting retry requested for {component.Component} ({history.Amount:C}).",
                ReservePostingStatus.Failed.ToString(), ReservePostingStatus.Pending.ToString(), history.ReserveHistoryId, "ReserveHistory"),
            cancellationToken);

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);

        var displayNames = await userDirectory.GetDisplayNamesAsync(ReserveTransactionMapper.UserIdsOf(history), cancellationToken);
        return ReserveTransactionMapper.ToDto(history, component.Component, displayNames);
    }
}
