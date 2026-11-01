using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.AddClaimParty;

public class AddClaimPartyCommandHandler(IClaimRepository claimRepository, IAuditLogService auditLogService)
    : IRequestHandler<AddClaimPartyCommand, ClaimPartyDto>
{
    public async Task<ClaimPartyDto> Handle(AddClaimPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var party = new ClaimParty
        {
            PartyRole = request.PartyRole,
            PartyType = request.PartyType,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            Email = request.Email,
            Phone = request.Phone,
        };
        claim.Parties.Add(party);

        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "PARTY_ADDED", $"Party added to claim {claim.ClaimNumber}: {request.PartyRole}."),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);

        return new ClaimPartyDto(
            party.ClaimPartyId, party.PartyRole.ToString(), party.PartyType.ToString(),
            party.FirstName, party.LastName, party.CompanyName, party.Email, party.Phone, party.IsActive);
    }
}
