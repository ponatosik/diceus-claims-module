using ClaimsModule.Application.Claims.Dtos;
using MediatR;

namespace ClaimsModule.Application.Claims.ApproveReserve;

/// <summary>POST /api/claims/{id}/reserves/{txnId}/approve — approves a PendingApproval reserve transaction (§10.2/BR-R-02/BR-R-03).</summary>
public record ApproveReserveCommand(Guid ClaimId, Guid ReserveHistoryId) : IRequest<ReserveTransactionDto>;
