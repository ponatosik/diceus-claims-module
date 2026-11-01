namespace ClaimsModule.Domain.Entities;

/// <summary>The loss incident linked to a claim, 1:1 (§9.2).</summary>
public class LossEvent : AuditableEntity
{
    public Guid LossEventId { get; set; }
    public Guid ClaimId { get; set; }
    public DateTimeOffset LossDate { get; set; }
    public string LossDescription { get; set; } = string.Empty;
    public string? LossLocation { get; set; }

    /// <summary>FK to CauseOfLossCodes.Code.</summary>
    public string CauseOfLossCode { get; set; } = string.Empty;

    public decimal? EstimatedLossAmount { get; set; }
    public DateTimeOffset ReportDate { get; set; }
    public string? PoliceReportNumber { get; set; }
}
