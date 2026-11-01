using ClaimsModule.Application.Common.Exceptions;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// Pure, database-free evaluation of the extra per-transition conditions from REQUIREMENTS.md §4.2/§4.3
/// that go beyond "is this transition in the state machine at all" (that part is
/// <see cref="ClaimStatusTransitions"/>). Handlers compute the booleans from real repository data and
/// pass them in here, which keeps this rule logic directly unit-testable without a database.
/// </summary>
public static class ClaimTransitionConditions
{
    /// <summary>BR-ST-02: Draft→Open requires no Critical issues and at least one active Claimant.</summary>
    public static IReadOnlyList<BlockingCondition> EvaluateOpenConditions(
        bool hasActiveClaimant, bool lossDateInFuture, bool lossDescriptionTooShort, bool causeOfLossCodeInactive)
    {
        var conditions = new List<BlockingCondition>();

        if (lossDateInFuture)
        {
            conditions.Add(new BlockingCondition("BR-C-01", "Loss date cannot be in the future."));
        }

        if (lossDescriptionTooShort)
        {
            conditions.Add(new BlockingCondition("BR-C-07", "Loss description is required and must be at least 20 characters."));
        }

        if (causeOfLossCodeInactive)
        {
            conditions.Add(new BlockingCondition("BR-C-05", "Cause of loss code is not recognised or is inactive."));
        }

        if (!hasActiveClaimant)
        {
            conditions.Add(new BlockingCondition("BR-ST-02", "At least one Claimant party is required to open a claim."));
        }

        return conditions;
    }

    /// <summary>BR-ST-03: Closed requires all four closure conditions from §4.3 (CC-01..CC-04).</summary>
    public static IReadOnlyList<BlockingCondition> EvaluateClosureConditions(
        bool anyReserveHistoryPendingApproval,
        bool lossDateInFuture,
        bool lossDescriptionTooShort,
        bool causeOfLossCodeInactive,
        bool hasActiveClaimant,
        bool anyReserveComponentNonZero,
        string? justificationNote)
    {
        var conditions = new List<BlockingCondition>();

        if (anyReserveHistoryPendingApproval)
        {
            conditions.Add(new BlockingCondition("CC-01", "No reserve components with status PendingApproval remain."));
        }

        if (lossDateInFuture || lossDescriptionTooShort || causeOfLossCodeInactive)
        {
            conditions.Add(new BlockingCondition("CC-02", "There are no unresolved Critical validation issues recorded against the claim."));
        }

        if (!hasActiveClaimant)
        {
            conditions.Add(new BlockingCondition("CC-03", "At least one ClaimParty of role Claimant exists on the claim."));
        }

        if (anyReserveComponentNonZero && string.IsNullOrWhiteSpace(justificationNote))
        {
            conditions.Add(new BlockingCondition("CC-04", "This claim has open reserves — provide a justification note to close it anyway."));
        }

        return conditions;
    }

    /// <summary>BR-ST-04: Closed→Reopened requires the Supervisor role and a non-empty reason.</summary>
    public static IReadOnlyList<BlockingCondition> EvaluateReopenConditions(string? role, string? reason)
    {
        var conditions = new List<BlockingCondition>();

        if (!string.Equals(role, "supervisor", StringComparison.OrdinalIgnoreCase))
        {
            conditions.Add(new BlockingCondition("BR-ST-04", "Reopening a claim requires the Supervisor role."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            conditions.Add(new BlockingCondition("BR-ST-04", "A reopen reason is required."));
        }

        return conditions;
    }

    /// <summary>Open/UnderInvestigation→PendingPayment requires at least one approved reserve transaction.</summary>
    public static IReadOnlyList<BlockingCondition> EvaluatePendingPaymentConditions(bool anyReserveApprovedOrAutoApproved) =>
        anyReserveApprovedOrAutoApproved
            ? []
            : [new BlockingCondition("TRANS-PENDINGPAYMENT-01", "At least one approved reserve component is required before moving to PendingPayment.")];
}
