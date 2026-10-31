namespace ClaimsModule.Domain;

/// <summary>Claim lifecycle status (§4.1). Valid transitions are enforced by application logic, not the schema.</summary>
public enum ClaimStatus
{
    Draft,
    Open,
    UnderInvestigation,
    PendingPayment,
    Closed,
    Reopened,
    Withdrawn,
}
