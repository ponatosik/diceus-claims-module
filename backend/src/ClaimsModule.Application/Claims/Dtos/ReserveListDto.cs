namespace ClaimsModule.Application.Claims.Dtos;

/// <summary>GET /api/claims/{id}/reserves — current balance per component plus the full transaction history, in one response (§10.2).</summary>
public record ReserveListDto(List<ReserveSummaryDto> Components, List<ReserveTransactionDto> History);
