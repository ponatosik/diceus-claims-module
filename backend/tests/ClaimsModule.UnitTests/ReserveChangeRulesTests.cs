using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Domain.Enums;
using FluentValidation.TestHelper;

namespace ClaimsModule.UnitTests;

/// <summary>§8 field rules for a reserve change (ADR-0011: "> 0" applies to Add; later changes are signed, non-zero).</summary>
public class ReserveChangeRulesTests
{
    private readonly ReserveChangeRules _rules = new();

    private static OpenReserveCommand Change(ReserveTransactionType type, decimal amount, ReserveComponentType component = ReserveComponentType.Indemnity) =>
        new() { Component = component, Amount = amount, ChangeReason = "Estimate", TransactionType = type };

    [Fact]
    public void An_Add_must_be_positive_except_for_subrogation_recoverable()
    {
        _rules.TestValidate(Change(ReserveTransactionType.Add, -1m)).ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage("Reserve amount must be greater than zero.");
        _rules.TestValidate(Change(ReserveTransactionType.Add, -1m, ReserveComponentType.SubrogationRecoverable)).ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void An_Adjust_may_be_negative_but_never_zero()
    {
        _rules.TestValidate(Change(ReserveTransactionType.Adjust, -1m)).ShouldNotHaveValidationErrorFor(x => x.Amount);
        _rules.TestValidate(Change(ReserveTransactionType.Adjust, 0m)).ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage("Reserve amount must not be zero.");
    }

    [Fact]
    public void The_FNOL_initial_reserve_always_counts_as_an_Add()
    {
        _rules.TestValidate(new CreateInitialReserveDto(ReserveComponentType.Expense, -10m, "x")).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void A_change_reason_is_required()
    {
        var change = Change(ReserveTransactionType.Add, 10m);
        change.ChangeReason = "";
        _rules.TestValidate(change).ShouldHaveValidationErrorFor(x => x.ChangeReason);
    }
}
