namespace ClaimsModule.Domain.Entities;

/// <summary>
/// A Demo User (GLOSSARY.md) — one of the three fixed simulated identities the dev token issuer can
/// act as (ADR-0010). Global reference data like <see cref="Policy"/>: seeded at migration time, not
/// tenant-scoped, never written by application code. Business rows reference users by plain id only.
/// </summary>
public class User
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>One of the §3 role codes: handler, supervisor, manager.</summary>
    public string Role { get; set; } = string.Empty;
}
