namespace ClaimsModule.API.Authentication;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. Per ADR-0007, this is what makes auth
/// "real validation against configuration-driven issuer/key material" rather than hardcoded —
/// swapping a real IdP in for an actual deployment is a config change, not a code change.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; set; }

    public required string Audience { get; set; }

    public required string SigningKey { get; set; }
}
