using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// Blocks reserve mutations (open, approve, reject, retract) on a claim whose status makes further
/// financial activity nonsensical. Not an explicit REQUIREMENTS.md rule — §4.1 only says "the status
/// controls which actions are permitted" without enumerating reserves specifically — but a Closed or
/// Withdrawn claim accepting a new auto-approved reserve (balance movement + GL posting job) is not
/// something a reasonable user of this system would expect to succeed.
/// </summary>
public static class ReserveClaimStatusGuard
{
    public static void EnsureMutable(Claim claim)
    {
        if (claim.Status is ClaimStatus.Closed or ClaimStatus.Withdrawn)
        {
            throw new BusinessRuleViolationException(
                [new BlockingCondition("RESERVE-CLAIM-STATUS-01", $"Reserves cannot be changed on a claim with status {claim.Status}.")]);
        }
    }
}
