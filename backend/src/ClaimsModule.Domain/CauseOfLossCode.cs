namespace ClaimsModule.Domain;

/// <summary>
/// Reference/lookup table for cause-of-loss codes (§9.9), seeded at migration time. Global reference
/// data, not tenant-scoped — unlike the business entities, it carries no OrganisationId or soft-delete
/// columns, matching its column list in §9.9 exactly.
/// </summary>
public class CauseOfLossCode
{
    public Guid CauseOfLossCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PerilCategory PerilCategory { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
