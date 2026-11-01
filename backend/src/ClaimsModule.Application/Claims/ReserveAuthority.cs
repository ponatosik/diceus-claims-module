namespace ClaimsModule.Application.Claims;

/// <summary>The three authority tiers from §6.3's table, keyed by transaction amount.</summary>
public enum ReserveAuthorityTier
{
    Auto,
    Supervisor,
    Manager,
}

/// <summary>
/// Pure, database-free reserve-authority logic (§6.3/BR-R-02). Tiers by <c>Math.Abs(amount)</c> — a
/// large negative SubrogationRecoverable transaction is exactly as consequential as a large positive
/// one and needs the same oversight (nothing in §6.3's table addresses sign; this is the only reading
/// that doesn't let a large negative transaction skip approval entirely).
/// </summary>
public static class ReserveAuthority
{
    public static ReserveAuthorityTier GetTier(decimal amount)
    {
        var absoluteAmount = Math.Abs(amount);

        if (absoluteAmount <= 10_000m)
        {
            return ReserveAuthorityTier.Auto;
        }

        return absoluteAmount <= 100_000m ? ReserveAuthorityTier.Supervisor : ReserveAuthorityTier.Manager;
    }

    /// <summary>Manager satisfies every tier (§3 — "all Supervisor capabilities"); Auto needs no approval call at all, but answers true if asked.</summary>
    public static bool CanApprove(ReserveAuthorityTier tier, string? role)
    {
        if (string.Equals(role, "manager", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return tier switch
        {
            ReserveAuthorityTier.Auto => true,
            ReserveAuthorityTier.Supervisor => string.Equals(role, "supervisor", StringComparison.OrdinalIgnoreCase),
            ReserveAuthorityTier.Manager => false,
            _ => false,
        };
    }
}
