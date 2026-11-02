namespace ClaimsModule.API.Authentication;

/// <summary>
/// ADR-0010: the dev token issuer is gated on this explicit flag — off by default, turned on only for
/// local dev, tests and the public demo deployment — not on the hosting environment's name.
/// </summary>
public class DevAuthOptions
{
    public const string SectionName = "DevAuth";

    public bool Enabled { get; set; }
}
