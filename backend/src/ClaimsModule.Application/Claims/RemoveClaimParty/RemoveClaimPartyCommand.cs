using MediatR;

namespace ClaimsModule.Application.Claims.RemoveClaimParty;

/// <summary>DELETE /api/claims/{id}/parties/{partyId} — soft-removes a party from the claim (sets IsActive = false) per §10.1.</summary>
public record RemoveClaimPartyCommand(Guid ClaimId, Guid PartyId) : IRequest;
