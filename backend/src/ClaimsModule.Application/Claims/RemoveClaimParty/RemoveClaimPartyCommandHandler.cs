using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.RemoveClaimParty;

public class RemoveClaimPartyCommandHandler(IClaimRepository claimRepository, IAuditLogService auditLogService)
    : IRequestHandler<RemoveClaimPartyCommand>
{
    public async Task Handle(RemoveClaimPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var party = claim.Parties.FirstOrDefault(p => p.ClaimPartyId == request.PartyId)
            ?? throw new NotFoundException("ClaimParty", request.PartyId);

        var activeClaimantCount = claim.Parties.Count(p => p.IsActive && p.PartyRole == PartyRole.Claimant);
        var isLastActiveClaimant = party.IsActive && party.PartyRole == PartyRole.Claimant && activeClaimantCount == 1;

        if (isLastActiveClaimant)
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("BR-P-01", "A claim must retain at least one active Claimant party.")]);
        }

        party.IsActive = false;

        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "PARTY_REMOVED", $"Party removed from claim {claim.ClaimNumber}: {party.PartyRole}.", RelatedEntityId: party.ClaimPartyId, RelatedEntityType: "ClaimParty"),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);
    }
}
