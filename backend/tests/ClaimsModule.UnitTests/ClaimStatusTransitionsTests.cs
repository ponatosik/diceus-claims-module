using ClaimsModule.Application.Claims;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.UnitTests;

public class ClaimStatusTransitionsTests
{
    // Every valid row from REQUIREMENTS.md §4.2.
    [Theory]
    [InlineData(ClaimStatus.Draft, ClaimStatus.Open)]
    [InlineData(ClaimStatus.Open, ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.Open, ClaimStatus.PendingPayment)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Withdrawn)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.Open)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn)]
    [InlineData(ClaimStatus.PendingPayment, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Reopened)]
    [InlineData(ClaimStatus.Reopened, ClaimStatus.Open)]
    public void Valid_transitions_are_allowed(ClaimStatus from, ClaimStatus to)
    {
        Assert.True(ClaimStatusTransitions.IsValidTransition(from, to));
        Assert.Contains(to, ClaimStatusTransitions.GetValidNextStatuses(from));
    }

    // A representative sample of every status pair NOT listed in §4.2, including the fully-terminal
    // Withdrawn status (every outgoing transition from it is invalid).
    [Theory]
    [InlineData(ClaimStatus.Draft, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Draft, ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.Draft, ClaimStatus.PendingPayment)]
    [InlineData(ClaimStatus.Draft, ClaimStatus.Withdrawn)]
    [InlineData(ClaimStatus.Draft, ClaimStatus.Reopened)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Draft)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Reopened)]
    [InlineData(ClaimStatus.PendingPayment, ClaimStatus.Open)]
    [InlineData(ClaimStatus.PendingPayment, ClaimStatus.Draft)]
    [InlineData(ClaimStatus.PendingPayment, ClaimStatus.Withdrawn)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Open)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Draft)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Reopened, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Reopened, ClaimStatus.Reopened)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.Open)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.Draft)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.PendingPayment)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.Reopened)]
    public void Invalid_transitions_are_rejected(ClaimStatus from, ClaimStatus to)
    {
        Assert.False(ClaimStatusTransitions.IsValidTransition(from, to));
        Assert.DoesNotContain(to, ClaimStatusTransitions.GetValidNextStatuses(from));
    }

    [Fact]
    public void Withdrawn_has_no_valid_next_statuses()
    {
        Assert.Empty(ClaimStatusTransitions.GetValidNextStatuses(ClaimStatus.Withdrawn));
    }
}
