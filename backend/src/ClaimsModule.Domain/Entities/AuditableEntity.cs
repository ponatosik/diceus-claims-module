namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Base for mutable, tenant-isolated business entities (§16.1): tenant isolation, audit columns,
/// and soft delete. Used by every entity except the append-only event logs (<see cref="AppendOnlyEntity"/>),
/// where nothing is ever updated or deleted.
/// </summary>
public abstract class AuditableEntity
{
    public Guid OrganisationId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UserCreated { get; set; }
    public Guid? UserModified { get; set; }
}
