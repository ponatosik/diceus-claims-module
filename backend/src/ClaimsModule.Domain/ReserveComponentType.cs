namespace ClaimsModule.Domain;

/// <summary>Classifies the nature of a reserve cost (§6.2).</summary>
public enum ReserveComponentType
{
    Indemnity,
    Expense,
    ALAE,
    SubrogationRecoverable,
}
