namespace ClaimsModule.Domain;

/// <summary>An asset or property affected by the loss, linked to a claim (§9.4).</summary>
public class ClaimRiskObject : AuditableEntity
{
    public Guid ClaimRiskObjectId { get; set; }
    public Guid ClaimId { get; set; }
    public AssetType AssetType { get; set; }
    public string AssetDescription { get; set; } = string.Empty;
    public string? DamageDescription { get; set; }
    public bool IsPrimary { get; set; }
    public string? AssetReference { get; set; }
}
