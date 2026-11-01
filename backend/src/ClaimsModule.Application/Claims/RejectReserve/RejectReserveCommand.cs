using ClaimsModule.Application.Claims.Dtos;
using MediatR;

namespace ClaimsModule.Application.Claims.RejectReserve;

/// <summary>POST /api/claims/{id}/reserves/{txnId}/reject — rejects a PendingApproval reserve transaction (§10.2/BR-R-04).</summary>
public record RejectReserveCommand(Guid ClaimId, Guid ReserveHistoryId, string RejectionReason) : IRequest<ReserveTransactionDto>;

/// <summary>The HTTP request body — ClaimId/ReserveHistoryId come from the route, not the body.</summary>
public record RejectReserveRequestBody(string RejectionReason);
