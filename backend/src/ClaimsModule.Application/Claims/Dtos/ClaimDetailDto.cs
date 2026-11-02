namespace ClaimsModule.Application.Claims.Dtos;

public record ClaimDetailDto(
    Guid ClaimId,
    string ClaimNumber,
    string Status,
    Guid? PolicyId,
    string? PolicyNumber,
    string? ClientName,
    DateTimeOffset ReportedDate,
    LossEventDto LossEvent,
    List<ClaimPartyDto> Parties,
    List<ClaimRiskObjectDto> RiskObjects,
    List<ReserveSummaryDto> Reserves,
    List<ClaimDocumentDto> Documents,
    List<AuditLogEntryDto> RecentAuditEntries,
    bool ReserveOverrideApproved,
    string? Severity,
    string? Notes,
    UserSummaryDto? AssignedHandler,
    string? CauseOfLossName);
