using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Claims.SetReserveOverride;

public class SetReserveOverrideCommandHandler(
    IClaimRepository claimRepository,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService) : IRequestHandler<SetReserveOverrideCommand>
{
    public async Task Handle(SetReserveOverrideCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        if (!string.Equals(currentUserService.Role, "manager", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleViolationException([new BlockingCondition("BR-R-05", "Only a Manager may set the reserve override flag.")]);
        }

        claim.ReserveOverrideApproved = true;

        // Not in §14.1's official event list — a reasonable addition for traceability, same judgment
        // call made for other unassigned-code cases in this slice and Slice B.
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "RESERVE_OVERRIDE_SET", $"Reserve aggregate-limit override set for claim {claim.ClaimNumber}."),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);
    }
}
