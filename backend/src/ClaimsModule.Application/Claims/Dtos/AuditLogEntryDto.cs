using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.Claims.Dtos;

/// <summary>
/// One ClaimAuditLog row for GET /api/claims/{id}/audit and ClaimDetailDto's recent entries. User and
/// related-entity fields back §11.3 Audit tab's "user" and "related entity link" (spec 006 story 13).
/// </summary>
public record AuditLogEntryDto(
    Guid AuditLogId,
    DateTimeOffset CreatedAt,
    string EventType,
    string Description,
    Guid? UserId,
    string? UserDisplayName,
    string? RelatedEntityType,
    Guid? RelatedEntityId);

public static class AuditLogEntryMapper
{
    public static List<AuditLogEntryDto> ToDtos(IEnumerable<ClaimAuditLog> entries, IReadOnlyDictionary<Guid, string> displayNames) =>
        [.. entries.Select(a => new AuditLogEntryDto(
            a.AuditLogId,
            a.CreatedAt,
            a.EventType,
            a.Description,
            a.CreatedByUserId,
            a.CreatedByUserId is { } userId ? displayNames.GetValueOrDefault(userId) : null,
            a.RelatedEntityType,
            a.RelatedEntityId))];
}
