using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.TransitionClaimStatus;

/// <summary>PUT /api/claims/{id}/status — transitions a claim's status, validated strictly against the state machine (§4.2/BR-ST-01).</summary>
public class TransitionClaimStatusCommand : IRequest<ClaimDto>
{
    public Guid ClaimId { get; set; }

    public ClaimStatus TargetStatus { get; set; }

    public string? Reason { get; set; }

    /// <summary>CC-04: required to proceed with closure when the claim still has open (nonzero) reserves.</summary>
    public string? JustificationNote { get; set; }
}
