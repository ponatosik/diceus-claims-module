using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.AddClaimParty;

/// <summary>POST /api/claims/{id}/parties — adds a party to an existing claim (§10.1).</summary>
public class AddClaimPartyCommand : IRequest<ClaimPartyDto>
{
    public Guid ClaimId { get; set; }

    public PartyRole PartyRole { get; set; }

    public PartyType PartyType { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? CompanyName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}
