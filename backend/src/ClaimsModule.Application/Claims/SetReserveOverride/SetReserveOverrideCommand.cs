using MediatR;

namespace ClaimsModule.Application.Claims.SetReserveOverride;

/// <summary>POST /api/claims/{id}/reserves/override — Manager sign-off that this claim's approved reserves may exceed $10,000,000 (BR-R-05).</summary>
public record SetReserveOverrideCommand(Guid ClaimId) : IRequest;
