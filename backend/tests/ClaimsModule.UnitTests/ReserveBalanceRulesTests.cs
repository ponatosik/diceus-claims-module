using ClaimsModule.Application.Claims;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.UnitTests;

/// <summary>ADR-0011: what a reserve transaction may do to its component's balance.</summary>
public class ReserveBalanceRulesTests
{
    private static IEnumerable<string> Codes(
        ReserveComponentType component, ReserveTransactionType type, bool componentExists, decimal balance, decimal amount) =>
        ReserveBalanceRules.Evaluate(component, type, componentExists, balance, amount).Select(c => c.Code);

    [Fact]
    public void Opening_a_component_requires_a_positive_amount_whatever_the_client_calls_the_transaction()
    {
        Assert.Equal(["BR-R-01"], Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Add, false, 0m, -500m));
        Assert.Equal(["RESERVE-TYPE-01"], Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Adjust, false, 0m, 500m));
        Assert.Empty(Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Add, false, 0m, 500m));
    }

    [Fact]
    public void A_subrogation_recoverable_component_may_open_negative()
    {
        Assert.Empty(Codes(ReserveComponentType.SubrogationRecoverable, ReserveTransactionType.Add, false, 0m, -500m));
    }

    [Fact]
    public void A_reversal_of_a_component_that_does_not_exist_is_rejected()
    {
        Assert.Equal(["RESERVE-TYPE-01"], Codes(ReserveComponentType.Expense, ReserveTransactionType.Reverse, false, 0m, -100m));
    }

    [Fact]
    public void An_adjustment_may_decrease_but_not_below_zero_except_for_subrogation()
    {
        Assert.Empty(Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Adjust, true, 8_000m, -8_000m));
        Assert.Equal(["RESERVE-BALANCE-NEGATIVE-01"], Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Adjust, true, 8_000m, -8_000.01m));
        Assert.Empty(Codes(ReserveComponentType.SubrogationRecoverable, ReserveTransactionType.Adjust, true, -1_000m, -5_000m));
    }

    [Fact]
    public void A_reversal_must_exactly_cancel_a_non_zero_balance()
    {
        Assert.Empty(Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Reverse, true, 6_000m, -6_000m));
        Assert.Empty(Codes(ReserveComponentType.SubrogationRecoverable, ReserveTransactionType.Reverse, true, -2_500m, 2_500m));
        Assert.Equal(["RESERVE-REVERSE-01"], Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Reverse, true, 6_000m, -5_000m));
        Assert.Equal(["RESERVE-REVERSE-01"], Codes(ReserveComponentType.Indemnity, ReserveTransactionType.Reverse, true, 0m, 0m));
    }
}
