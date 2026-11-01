namespace ClaimsModule.Application.Common;

/// <summary>Input to <see cref="IAuditLogService.LogAsync"/> — one row to append to ClaimAuditLog (§9.8/AUD-02).</summary>
public record AuditLogEntry(
    Guid ClaimId,
    string EventType,
    string Description,
    string? OldValue = null,
    string? NewValue = null,
    Guid? RelatedEntityId = null,
    string? RelatedEntityType = null);
