namespace ClaimsModule.Domain.Enums;

/// <summary>Claim severity classification (§9.1).</summary>
public enum ClaimSeverity
{
    Minor,
    Standard,
    Critical,
    Catastrophic,
}
