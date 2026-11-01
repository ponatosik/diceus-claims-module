using ClaimsModule.Application.Common.Exceptions;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// Pure, database-free BR-R-05 aggregate-limit check — the $10,000,000 limit is on *projected*
/// approved totals, so handlers (OpenReserveCommand's auto-approve branch, ApproveReserveCommand)
/// compute the real numbers and pass them in, exactly like Slice B's ClaimTransitionConditions.
/// </summary>
public static class ReserveAggregateLimit
{
    private const decimal LimitAmount = 10_000_000m;

    public static IReadOnlyList<BlockingCondition> Evaluate(
        decimal currentTotalAcrossComponents, decimal transactionAmount, bool overrideApproved)
    {
        var projectedTotal = currentTotalAcrossComponents + transactionAmount;

        if (Math.Abs(projectedTotal) > LimitAmount && !overrideApproved)
        {
            return [new BlockingCondition(
                "BR-R-05",
                "Total approved reserves will exceed $10,000,000. A Manager must set the override flag on this claim before this reserve can be approved.")];
        }

        return [];
    }
}
