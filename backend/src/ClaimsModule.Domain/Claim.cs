namespace ClaimsModule.Domain;

/// <summary>The central aggregate root — one row per claim (§9.1).</summary>
public class Claim : AuditableEntity
{
    public Guid ClaimId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;

    /// <summary>FK to the simulated Policy table. Nullable — a claim may be created with an unknown policy (BR-C-06).</summary>
    public Guid? PolicyId { get; set; }

    /// <summary>Denormalised for display; populated from the linked policy.</summary>
    public string? PolicyNumber { get; set; }

    /// <summary>Denormalised for display; populated from the linked policy.</summary>
    public string? ClientName { get; set; }

    public ClaimStatus Status { get; set; }
    public ClaimSeverity? Severity { get; set; }
    public DateTimeOffset ReportedDate { get; set; }
    public Guid? AssignedHandlerId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosureReason { get; set; }
    public string? Notes { get; set; }

    /// <summary>Optimistic concurrency token (§16.1 — RowVer required on aggregate roots).</summary>
    public byte[] RowVer { get; set; } = [];

    /// <summary>
    /// Navigation collections for child entities, used so EF Core can resolve insert order and
    /// backfill the database-generated ClaimId across the whole graph in one SaveChanges call —
    /// e.g. Claim + LossEvent + Parties + RiskObjects created together in a single FNOL transaction
    /// (§10.1, US13). Populate these rather than setting a child's ClaimId scalar directly when the
    /// claim itself hasn't been saved yet.
    /// </summary>
    public LossEvent? LossEvent { get; set; }
    public ICollection<ClaimParty> Parties { get; set; } = [];
    public ICollection<ClaimRiskObject> RiskObjects { get; set; } = [];
}
