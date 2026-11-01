namespace ClaimsModule.Application.Claims.Dtos;

public record ClaimDto(
    Guid ClaimId,
    string ClaimNumber,
    string Status,
    Guid? PolicyId,
    string? PolicyNumber,
    string? ClientName,
    DateTimeOffset ReportedDate,
    LossEventDto LossEvent,
    List<ClaimPartyDto> Parties,
    List<ClaimRiskObjectDto> RiskObjects);
