namespace ClaimsModule.Domain;

/// <summary>
/// A reserve component on a claim (§9.5). <see cref="CurrentAmount"/> is maintained via sequential
/// delta tracking against <see cref="ReserveHistory"/> (ADR-0002) — it is never recomputed via SUM().
/// </summary>
public class ClaimReserveComponent : AuditableEntity
{
    public Guid ReserveComponentId { get; set; }
    public Guid ClaimId { get; set; }

    /// <summary>
    /// Set this (not <see cref="ClaimId"/> directly) when the parent <see cref="Claim"/> hasn't been
    /// saved yet — e.g. creating a claim with an initial reserve in one transaction. ClaimId is a
    /// database-generated (NEWSEQUENTIALID()) key, so EF Core can only resolve insert order and
    /// backfill the FK through the navigation graph, not a scalar value read before SaveChanges runs.
    /// </summary>
    public Claim? Claim { get; set; }

    public ReserveComponentType Component { get; set; }
    public decimal CurrentAmount { get; set; }
    public ReserveComponentStatus Status { get; set; }
    public string? Notes { get; set; }

    /// <summary>Optimistic concurrency token (§16.1 — RowVer required on aggregate roots).</summary>
    public byte[] RowVer { get; set; } = [];
}
