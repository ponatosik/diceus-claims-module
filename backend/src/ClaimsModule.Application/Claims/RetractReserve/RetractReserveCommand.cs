using ClaimsModule.Application.Claims.Dtos;
using MediatR;

namespace ClaimsModule.Application.Claims.RetractReserve;

/// <summary>POST /api/claims/{id}/reserves/{txnId}/retract — the submitter cancels their own pending reserve before resolution (§10.2/user story 10).</summary>
public record RetractReserveCommand(Guid ClaimId, Guid ReserveHistoryId) : IRequest<ReserveTransactionDto>;
