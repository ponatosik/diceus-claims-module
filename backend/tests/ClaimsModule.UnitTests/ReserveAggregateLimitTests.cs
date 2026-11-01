using ClaimsModule.Application.Claims;

namespace ClaimsModule.UnitTests;

public class ReserveAggregateLimitTests
{
    [Fact]
    public void Exactly_10_million_projected_total_is_not_over_the_limit()
    {
        var result = ReserveAggregateLimit.Evaluate(currentTotalAcrossComponents: 9_000_000m, transactionAmount: 1_000_000m, overrideApproved: false);

        Assert.Empty(result);
    }

    [Fact]
    public void Just_over_10_million_without_override_is_blocked_with_BR_R_05()
    {
        var result = ReserveAggregateLimit.Evaluate(currentTotalAcrossComponents: 9_000_000m, transactionAmount: 1_000_000.01m, overrideApproved: false);

        Assert.Equal(["BR-R-05"], result.Select(c => c.Code));
    }

    [Fact]
    public void Just_over_10_million_with_override_approved_is_not_blocked()
    {
        var result = ReserveAggregateLimit.Evaluate(currentTotalAcrossComponents: 9_000_000m, transactionAmount: 1_000_000.01m, overrideApproved: true);

        Assert.Empty(result);
    }

    [Fact]
    public void The_limit_is_symmetric_for_a_large_negative_projected_total()
    {
        var result = ReserveAggregateLimit.Evaluate(currentTotalAcrossComponents: -9_000_000m, transactionAmount: -1_000_001m, overrideApproved: false);

        Assert.Equal(["BR-R-05"], result.Select(c => c.Code));
    }
}
