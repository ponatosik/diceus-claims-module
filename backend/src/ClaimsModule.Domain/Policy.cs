namespace ClaimsModule.Domain;

/// <summary>
/// Minimal simulated policy table (§9.10, §5.5) standing in for a real Policy Administration System
/// module. Global reference data, not tenant-scoped, matching its column list in §9.10 exactly.
/// </summary>
public class Policy
{
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public PolicyStatus Status { get; set; }

    /// <summary>Comma-separated list of coverage type names.</summary>
    public string CoverageTypes { get; set; } = string.Empty;
}
