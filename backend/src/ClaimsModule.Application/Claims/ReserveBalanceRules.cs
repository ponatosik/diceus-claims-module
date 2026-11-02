using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// Pure, database-free rules on what a reserve transaction does to its component's balance (ADR-0011):
/// only SubrogationRecoverable may go below zero (§6.2 "May Go Negative?"), and a Reverse must exactly
/// cancel the balance. Checked when a transaction is submitted and again when it is approved, because
/// the balance it applies to can move in between.
/// </summary>
public static class ReserveBalanceRules
{
    public static IReadOnlyList<BlockingCondition> Evaluate(
        ReserveComponentType component, ReserveTransactionType transactionType, decimal currentBalance, decimal amount)
    {
        if (transactionType == ReserveTransactionType.Reverse && (currentBalance == 0 || amount != -currentBalance))
        {
            return [new BlockingCondition(
                "RESERVE-REVERSE-01",
                $"A reversal must exactly cancel the {component} reserve's current balance of {currentBalance:C}.")];
        }

        if (component != ReserveComponentType.SubrogationRecoverable && currentBalance + amount < 0)
        {
            return [new BlockingCondition(
                "RESERVE-BALANCE-NEGATIVE-01",
                $"The {component} reserve may not go below zero (current balance {currentBalance:C}).")];
        }

        return [];
    }
}
