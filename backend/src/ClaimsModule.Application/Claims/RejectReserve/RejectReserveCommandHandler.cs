using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.RejectReserve;

public class RejectReserveCommandHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService) : IRequestHandler<RejectReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RejectReserveCommand request, CancellationToken cancellationToken)
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
                [new BlockingCondition("RESERVE-NOT-PENDING-01", "Only a reserve transaction in PendingApproval status can be rejected.")]);
        }

        // No amount-tier distinction for rejection, unlike approval — any Supervisor or Manager may
        // reject any pending amount.
        var role = currentUserService.Role;
        var hasAuthority = string.Equals(role, "supervisor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "manager", StringComparison.OrdinalIgnoreCase);
        if (!hasAuthority)
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("BR-R-02", "Your role does not have authority to reject this reserve.")]);
        }

        var component = await reserveComponentRepository.GetByIdAsync(history.ReserveComponentId, cancellationToken)
            ?? throw new NotFoundException("ClaimReserveComponent", history.ReserveComponentId);

        history.ApprovalStatus = ReserveApprovalStatus.Rejected;
        history.RejectedByUserId = currentUserService.UserId;
        history.RejectedAt = DateTimeOffset.UtcNow;
        history.RejectionReason = request.RejectionReason;

        await auditLogService.LogAsync(
            new AuditLogEntry(
                history.ClaimId, "RESERVE_REJECTED", $"Reserve rejected: {component.Component} ({history.Amount:C}).",
                request.RejectionReason, null, history.ReserveHistoryId, "ReserveHistory"),
            cancellationToken);

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);

        return ReserveTransactionMapper.ToDto(history, component.Component);
    }
}
