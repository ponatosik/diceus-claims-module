namespace ClaimsModule.Persistence;

/// <summary>
/// Fixed identifiers used by migration-time seed data (§16.4 — seeding happens via EF Core <c>HasData</c>,
/// never at application startup, and <c>HasData</c> requires deterministic key values).
/// </summary>
/// <remarks>
/// The single fixed <see cref="OrganisationId"/> this greenfield system runs under. Application/handler
/// code (from Slice B onward) must read this from configuration at runtime, never hardcode it inline
/// (NFR-CONST-04) — this constant exists solely so migration generation has something deterministic
/// to seed tenant-scoped rows against, and configuration should be set to match it.
/// </remarks>
public static class SeedData
{
    public static readonly Guid OrganisationId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // ADR-0010's fixed Demo Users. The frontend's MockAuthService mirrors these ids exactly.
    public static readonly Guid DemoHandlerUserId = Guid.Parse("00000000-0000-0000-0003-000000000001");
    public static readonly Guid DemoSupervisorUserId = Guid.Parse("00000000-0000-0000-0003-000000000002");
    public static readonly Guid DemoManagerUserId = Guid.Parse("00000000-0000-0000-0003-000000000003");
}
