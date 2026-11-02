using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.RetractReserve;

public class RetractReserveCommandHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    IUserDirectory userDirectory) : IRequestHandler<RetractReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RetractReserveCommand request, CancellationToken cancellationToken)
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
                [new BlockingCondition("RESERVE-NOT-PENDING-01", "Only a reserve transaction in PendingApproval status can be retracted.")]);
        }

        if (currentUserService.UserId is null || currentUserService.UserId != history.SubmittedByUserId)
        {
            // Distinct from RESERVE-NOT-PENDING-01: the transaction IS pending, the caller just isn't
            // the one who may act on it — a frontend needs to tell these two apart (reviewer finding).
            throw new BusinessRuleViolationException(
                [new BlockingCondition("RESERVE-RETRACT-FORBIDDEN-01", "Only the submitter may retract their own pending reserve.")]);
        }

        var component = await reserveComponentRepository.GetByIdAsync(history.ReserveComponentId, cancellationToken)
            ?? throw new NotFoundException("ClaimReserveComponent", history.ReserveComponentId);

        history.ApprovalStatus = ReserveApprovalStatus.Cancelled;

        await auditLogService.LogAsync(
            new AuditLogEntry(
                history.ClaimId, "RESERVE_RETRACTED", $"Reserve retracted: {component.Component} ({AuditMoney.Format(history.Amount)}).",
                null, null, history.ReserveHistoryId, "ReserveHistory"),
            cancellationToken);

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);

        var displayNames = await userDirectory.GetDisplayNamesAsync(ReserveTransactionMapper.UserIdsOf(history), cancellationToken);
        return ReserveTransactionMapper.ToDto(history, component.Component, displayNames);
    }
}
