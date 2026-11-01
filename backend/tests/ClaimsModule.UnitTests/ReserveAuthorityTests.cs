using ClaimsModule.Application.Claims;

namespace ClaimsModule.UnitTests;

public class ReserveAuthorityTests
{
    [Theory]
    [InlineData(10_000d, ReserveAuthorityTier.Auto)]
    [InlineData(10_000.01d, ReserveAuthorityTier.Supervisor)]
    [InlineData(100_000d, ReserveAuthorityTier.Supervisor)]
    [InlineData(100_000.01d, ReserveAuthorityTier.Manager)]
    [InlineData(-10_000.01d, ReserveAuthorityTier.Supervisor)]
    public void GetTier_tiers_by_absolute_value_at_the_exact_boundaries(double amount, ReserveAuthorityTier expected)
    {
        Assert.Equal(expected, ReserveAuthority.GetTier((decimal)amount));
    }

    [Theory]
    [InlineData(ReserveAuthorityTier.Auto, "handler", true)]
    [InlineData(ReserveAuthorityTier.Auto, null, true)]
    [InlineData(ReserveAuthorityTier.Supervisor, "handler", false)]
    [InlineData(ReserveAuthorityTier.Supervisor, "supervisor", true)]
    [InlineData(ReserveAuthorityTier.Supervisor, "SUPERVISOR", true)]
    [InlineData(ReserveAuthorityTier.Supervisor, "manager", true)]
    [InlineData(ReserveAuthorityTier.Supervisor, null, false)]
    [InlineData(ReserveAuthorityTier.Manager, "handler", false)]
    [InlineData(ReserveAuthorityTier.Manager, "supervisor", false)]
    [InlineData(ReserveAuthorityTier.Manager, "manager", true)]
    [InlineData(ReserveAuthorityTier.Manager, "MANAGER", true)]
    [InlineData(ReserveAuthorityTier.Manager, null, false)]
    public void CanApprove_matches_the_authority_table(ReserveAuthorityTier tier, string? role, bool expected)
    {
        Assert.Equal(expected, ReserveAuthority.CanApprove(tier, role));
    }
}
