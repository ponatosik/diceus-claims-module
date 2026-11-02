namespace ClaimsModule.Application.Claims.Dtos;

public record ClaimSummaryDto(
    Guid ClaimId,
    string ClaimNumber,
    string? ClientName,
    string? PolicyNumber,
    DateTimeOffset LossDate,
    string Status,
    decimal TotalReserves,
    string? CauseOfLossCode,
    string? CauseOfLossName);
