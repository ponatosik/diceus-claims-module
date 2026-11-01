using ClaimsModule.Application.Claims;

namespace ClaimsModule.UnitTests;

public class ClaimTransitionConditionsTests
{
    [Fact]
    public void EvaluateOpenConditions_returns_empty_when_all_clear()
    {
        var result = ClaimTransitionConditions.EvaluateOpenConditions(
            hasActiveClaimant: true, lossDateInFuture: false, lossDescriptionTooShort: false, causeOfLossCodeInactive: false);

        Assert.Empty(result);
    }

    [Fact]
    public void EvaluateOpenConditions_flags_missing_claimant_as_BR_ST_02()
    {
        var result = ClaimTransitionConditions.EvaluateOpenConditions(
            hasActiveClaimant: false, lossDateInFuture: false, lossDescriptionTooShort: false, causeOfLossCodeInactive: false);

        Assert.Equal(["BR-ST-02"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateOpenConditions_flags_future_loss_date_as_BR_C_01()
    {
        var result = ClaimTransitionConditions.EvaluateOpenConditions(
            hasActiveClaimant: true, lossDateInFuture: true, lossDescriptionTooShort: false, causeOfLossCodeInactive: false);

        Assert.Equal(["BR-C-01"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateOpenConditions_flags_short_description_as_BR_C_07()
    {
        var result = ClaimTransitionConditions.EvaluateOpenConditions(
            hasActiveClaimant: true, lossDateInFuture: false, lossDescriptionTooShort: true, causeOfLossCodeInactive: false);

        Assert.Equal(["BR-C-07"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateOpenConditions_flags_inactive_cause_of_loss_code_as_BR_C_05()
    {
        var result = ClaimTransitionConditions.EvaluateOpenConditions(
            hasActiveClaimant: true, lossDateInFuture: false, lossDescriptionTooShort: false, causeOfLossCodeInactive: true);

        Assert.Equal(["BR-C-05"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateClosureConditions_returns_empty_when_all_clear()
    {
        var result = ClaimTransitionConditions.EvaluateClosureConditions(
            anyReserveHistoryPendingApproval: false, lossDateInFuture: false, lossDescriptionTooShort: false,
            causeOfLossCodeInactive: false, hasActiveClaimant: true, anyReserveComponentNonZero: false, justificationNote: null);

        Assert.Empty(result);
    }

    [Fact]
    public void EvaluateClosureConditions_flags_pending_approval_reserve_as_CC_01()
    {
        var result = ClaimTransitionConditions.EvaluateClosureConditions(
            anyReserveHistoryPendingApproval: true, lossDateInFuture: false, lossDescriptionTooShort: false,
            causeOfLossCodeInactive: false, hasActiveClaimant: true, anyReserveComponentNonZero: false, justificationNote: null);

        Assert.Equal(["CC-01"], result.Select(c => c.Code));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void EvaluateClosureConditions_flags_an_unresolved_critical_issue_as_CC_02(
        bool lossDateInFuture, bool lossDescriptionTooShort, bool causeOfLossCodeInactive)
    {
        var result = ClaimTransitionConditions.EvaluateClosureConditions(
            anyReserveHistoryPendingApproval: false, lossDateInFuture, lossDescriptionTooShort,
            causeOfLossCodeInactive, hasActiveClaimant: true, anyReserveComponentNonZero: false, justificationNote: null);

        Assert.Equal(["CC-02"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateClosureConditions_flags_missing_claimant_as_CC_03()
    {
        var result = ClaimTransitionConditions.EvaluateClosureConditions(
            anyReserveHistoryPendingApproval: false, lossDateInFuture: false, lossDescriptionTooShort: false,
            causeOfLossCodeInactive: false, hasActiveClaimant: false, anyReserveComponentNonZero: false, justificationNote: null);

        Assert.Equal(["CC-03"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateClosureConditions_flags_open_reserve_without_justification_as_CC_04()
    {
        var result = ClaimTransitionConditions.EvaluateClosureConditions(
            anyReserveHistoryPendingApproval: false, lossDateInFuture: false, lossDescriptionTooShort: false,
            causeOfLossCodeInactive: false, hasActiveClaimant: true, anyReserveComponentNonZero: true, justificationNote: null);

        Assert.Equal(["CC-04"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateClosureConditions_CC_04_is_cleared_by_a_non_blank_justification_note()
    {
        var result = ClaimTransitionConditions.EvaluateClosureConditions(
            anyReserveHistoryPendingApproval: false, lossDateInFuture: false, lossDescriptionTooShort: false,
            causeOfLossCodeInactive: false, hasActiveClaimant: true, anyReserveComponentNonZero: true, justificationNote: "approved by ops");

        Assert.Empty(result);
    }

    [Fact]
    public void EvaluateReopenConditions_returns_empty_for_supervisor_with_a_reason()
    {
        var result = ClaimTransitionConditions.EvaluateReopenConditions(role: "supervisor", reason: "customer disputed settlement");

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("SUPERVISOR")]
    [InlineData("Supervisor")]
    public void EvaluateReopenConditions_role_check_is_case_insensitive(string role)
    {
        var result = ClaimTransitionConditions.EvaluateReopenConditions(role, reason: "customer disputed settlement");

        Assert.Empty(result);
    }

    [Fact]
    public void EvaluateReopenConditions_flags_wrong_role_as_BR_ST_04()
    {
        var result = ClaimTransitionConditions.EvaluateReopenConditions(role: "handler", reason: "customer disputed settlement");

        Assert.Equal(["BR-ST-04"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateReopenConditions_flags_empty_reason_as_BR_ST_04()
    {
        var result = ClaimTransitionConditions.EvaluateReopenConditions(role: "supervisor", reason: "   ");

        Assert.Equal(["BR-ST-04"], result.Select(c => c.Code));
    }

    [Fact]
    public void EvaluateReopenConditions_flags_both_wrong_role_and_empty_reason_as_two_entries()
    {
        var result = ClaimTransitionConditions.EvaluateReopenConditions(role: "handler", reason: null);

        Assert.Equal(["BR-ST-04", "BR-ST-04"], result.Select(c => c.Code));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EvaluatePendingPaymentConditions_returns_empty_when_a_reserve_is_approved()
    {
        var result = ClaimTransitionConditions.EvaluatePendingPaymentConditions(anyReserveApprovedOrAutoApproved: true);

        Assert.Empty(result);
    }

    [Fact]
    public void EvaluatePendingPaymentConditions_flags_no_approved_reserve_as_TRANS_PENDINGPAYMENT_01()
    {
        var result = ClaimTransitionConditions.EvaluatePendingPaymentConditions(anyReserveApprovedOrAutoApproved: false);

        Assert.Equal(["TRANS-PENDINGPAYMENT-01"], result.Select(c => c.Code));
    }
}
