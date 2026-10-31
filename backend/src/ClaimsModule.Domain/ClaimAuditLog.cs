namespace ClaimsModule.Domain;

/// <summary>
/// Immutable append-only event log (§9.8, BR-A-01). The sole write path is IAuditLogService
/// (Application layer, implemented in a later slice) — no handler writes here directly.
/// </summary>
public class ClaimAuditLog : AppendOnlyEntity
{
    public Guid AuditLogId { get; set; }
    public Guid ClaimId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
