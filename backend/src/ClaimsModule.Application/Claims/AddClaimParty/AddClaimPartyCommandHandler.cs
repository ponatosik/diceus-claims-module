using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.AddClaimParty;

public class AddClaimPartyCommandHandler(IClaimRepository claimRepository, IAuditLogService auditLogService)
    : IRequestHandler<AddClaimPartyCommand, ClaimPartyDto>
{
    public async Task<ClaimPartyDto> Handle(AddClaimPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        // Client-side id (as for claims and reserves) so the audit entry can link the party in the same commit.
        var party = new ClaimParty
        {
            ClaimPartyId = Guid.NewGuid(),
            ClaimId = claim.ClaimId,
            PartyRole = request.PartyRole,
            PartyType = request.PartyType,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            Email = request.Email,
            Phone = request.Phone,
        };
        claimRepository.AddParty(party);

        await auditLogService.LogAsync(
            new AuditLogEntry(
                claim.ClaimId, "PARTY_ADDED", $"Party added to claim {claim.ClaimNumber}: {request.PartyRole}.",
                null, null, party.ClaimPartyId, "ClaimParty"),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);

        return new ClaimPartyDto(
            party.ClaimPartyId, party.PartyRole.ToString(), party.PartyType.ToString(),
            party.FirstName, party.LastName, party.CompanyName, party.Email, party.Phone, party.IsActive);
    }
}
