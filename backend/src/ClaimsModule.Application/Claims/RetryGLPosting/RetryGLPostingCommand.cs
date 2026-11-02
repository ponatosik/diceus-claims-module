using ClaimsModule.Application.Claims.Dtos;
using MediatR;

namespace ClaimsModule.Application.Claims.RetryGLPosting;

/// <summary>POST /api/claims/{id}/reserves/{txnId}/retry-posting — §11.3's retry button for a Failed GL posting (spec 006 story 11).</summary>
public record RetryGLPostingCommand(Guid ClaimId, Guid ReserveHistoryId) : IRequest<ReserveTransactionDto>;
