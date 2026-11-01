using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.OpenReserve;

/// <summary>POST /api/claims/{id}/reserves — opens a new reserve component or adjusts an existing one (§10.2); distinguished by TransactionType.</summary>
public class OpenReserveCommand : IRequest<ReserveTransactionDto>
{
    public Guid ClaimId { get; set; }

    public ReserveComponentType Component { get; set; }

    public decimal Amount { get; set; }

    public string ChangeReason { get; set; } = string.Empty;

    public ReserveTransactionType TransactionType { get; set; }
}
