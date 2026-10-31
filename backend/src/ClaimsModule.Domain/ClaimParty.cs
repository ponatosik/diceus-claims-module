namespace ClaimsModule.Domain;

/// <summary>A person or organisation involved in a claim (§9.3).</summary>
public class ClaimParty : AuditableEntity
{
    public Guid ClaimPartyId { get; set; }
    public Guid ClaimId { get; set; }
    public PartyRole PartyRole { get; set; }
    public PartyType PartyType { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }

    /// <summary>Soft-remove from the claim (BR-P-01 — a claim must always retain at least one active Claimant).</summary>
    public bool IsActive { get; set; } = true;
}
