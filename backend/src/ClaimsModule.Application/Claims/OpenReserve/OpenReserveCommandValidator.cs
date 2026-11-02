using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using FluentValidation;

namespace ClaimsModule.Application.Claims.OpenReserve;

/// <summary>§8/BR-R-01 validation rules for opening or adjusting a reserve.</summary>
public class OpenReserveCommandValidator : AbstractValidator<OpenReserveCommand>
{
    public OpenReserveCommandValidator(IClaimRepository claimRepository)
    {
        Include(new ReserveChangeRules());
        RuleFor(x => x.TransactionType).IsInEnum().WithMessage("Invalid reserve transaction type.");

        // BR-C-06's consequence: unlike Slice B's identical Warning at claim-creation time, a missing
        // policy link is a hard block here — "reserve creation is blocked until a policy is linked."
        RuleFor(x => x.ClaimId)
            .MustAsync(async (claimId, cancellationToken) =>
            {
                var claim = await claimRepository.GetByIdAsync(claimId, cancellationToken);
                return claim?.PolicyId is not null;
            })
            .WithMessage("No policy linked. Policy must be associated before reserves can be set.");
    }
}
