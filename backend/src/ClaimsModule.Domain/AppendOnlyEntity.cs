namespace ClaimsModule.Domain;

/// <summary>
/// Base for append-only event-log entities (ReserveHistory, ClaimAuditLog, OutboxMessage): tenant
/// isolation and a creation timestamp only. No soft delete, no update tracking — nothing ever
/// updates or deletes a row in these tables (BR-A-01, §6.6).
/// </summary>
public abstract class AppendOnlyEntity
{
    public Guid OrganisationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
