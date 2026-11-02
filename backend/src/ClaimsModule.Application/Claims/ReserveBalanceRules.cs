using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// Pure, database-free rules on what a reserve transaction does to its component (ADR-0011). Whether a
/// transaction *opens* a component is decided here from whether the component exists — not from the
/// client's transaction type — so BR-R-01 can't be bypassed by labelling a negative opening "Adjust".
/// Checked when a transaction is submitted and again when it is approved, because the balance it
/// applies to can move in between.
/// </summary>
public static class ReserveBalanceRules
{
    public const string OpeningMustBePositive = "BR-R-01";
    public const string NothingToChange = "RESERVE-TYPE-01";
    public const string ReversalMustCancelBalance = "RESERVE-REVERSE-01";
    public const string BalanceMayNotGoNegative = "RESERVE-BALANCE-NEGATIVE-01";

    public static IReadOnlyList<BlockingCondition> Evaluate(
        ReserveComponentType component,
        ReserveTransactionType transactionType,
        bool componentExists,
        decimal currentBalance,
        decimal amount)
    {
        var mayGoNegative = component == ReserveComponentType.SubrogationRecoverable; // §6.2

        if (!componentExists)
        {
            if (transactionType != ReserveTransactionType.Add)
            {
                return [new BlockingCondition(
                    NothingToChange,
                    $"There is no {component} reserve to {transactionType.ToString().ToLowerInvariant()} — open it with an Add first.")];
            }

            return amount > 0 || (mayGoNegative && amount != 0)
                ? []
                : [new BlockingCondition(OpeningMustBePositive, "Reserve amount must be greater than zero.")];
        }

        if (transactionType == ReserveTransactionType.Reverse && (currentBalance == 0 || amount != -currentBalance))
        {
            return [new BlockingCondition(
                ReversalMustCancelBalance,
                $"A reversal must exactly cancel the {component} reserve's current balance of {UsdAmount.Format(currentBalance)}.")];
        }

        if (!mayGoNegative && currentBalance + amount < 0)
        {
            return [new BlockingCondition(
                BalanceMayNotGoNegative,
                $"The {component} reserve may not go below zero (current balance {UsdAmount.Format(currentBalance)}).")];
        }

        return [];
    }

    /// <summary>Throws a 422 (ADR-0008 blockingConditions) when <see cref="Evaluate"/> finds any violation.</summary>
    public static void EnsureSatisfied(
        ReserveComponentType component,
        ReserveTransactionType transactionType,
        bool componentExists,
        decimal currentBalance,
        decimal amount)
    {
        var conditions = Evaluate(component, transactionType, componentExists, currentBalance, amount);
        if (conditions.Count > 0)
        {
            throw new BusinessRuleViolationException(conditions);
        }
    }
}
