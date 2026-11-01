namespace ClaimsModule.Application.Claims.Dtos;

public record LossEventDto(
    DateTimeOffset LossDate,
    string LossDescription,
    string? LossLocation,
    string CauseOfLossCode,
    decimal? EstimatedLossAmount);
