namespace ClaimsModule.Application.Claims.Dtos;

public record ReserveSummaryDto(Guid ReserveComponentId, string Component, decimal CurrentAmount, string Status);
