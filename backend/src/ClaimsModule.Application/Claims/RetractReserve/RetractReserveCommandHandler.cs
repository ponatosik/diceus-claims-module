using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.RetractReserve;

public class RetractReserveCommandHandler(
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService) : IRequestHandler<RetractReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RetractReserveCommand request, CancellationToken cancellationToken)
    {
        var history = await reserveComponentRepository.GetHistoryByIdAsync(request.ReserveHistoryId, cancellationToken);
        if (history is null || history.ClaimId != request.ClaimId)
        {
            throw new NotFoundException("ReserveHistory", request.ReserveHistoryId);
        }

        if (history.ApprovalStatus != ReserveApprovalStatus.PendingApproval)
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("RESERVE-NOT-PENDING-01", "Only a reserve transaction in PendingApproval status can be retracted.")]);
        }

        if (currentUserService.UserId is null || currentUserService.UserId != history.SubmittedByUserId)
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("RESERVE-NOT-PENDING-01", "Only the submitter may retract their own pending reserve.")]);
        }

        var component = await reserveComponentRepository.GetByIdAsync(history.ReserveComponentId, cancellationToken)
            ?? throw new NotFoundException("ClaimReserveComponent", history.ReserveComponentId);

        history.ApprovalStatus = ReserveApprovalStatus.Cancelled;

        await auditLogService.LogAsync(
            new AuditLogEntry(
                history.ClaimId, "RESERVE_RETRACTED", $"Reserve retracted: {component.Component} ({history.Amount:C}).",
                null, null, history.ReserveHistoryId, "ReserveHistory"),
            cancellationToken);

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);

        return ReserveTransactionMapper.ToDto(history, component.Component);
    }
}
