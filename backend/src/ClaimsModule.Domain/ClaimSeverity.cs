namespace ClaimsModule.Domain;

/// <summary>Claim severity classification (§9.1).</summary>
public enum ClaimSeverity
{
    Minor,
    Standard,
    Critical,
    Catastrophic,
}
