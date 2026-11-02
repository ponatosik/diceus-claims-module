using ClaimsModule.Domain.Enums;
using FluentValidation;

namespace ClaimsModule.Application.Claims;

/// <summary>The fields every reserve change carries, whether submitted on its own or as FNOL's initial reserve.</summary>
public interface IReserveChangeRequest
{
    ReserveComponentType Component { get; }

    decimal Amount { get; }

    string ChangeReason { get; }
}

/// <summary>
/// §8/BR-R-01 field rules for a reserve change — included by <c>OpenReserveCommandValidator</c> and
/// applied to <c>CreateClaimCommand.InitialReserve</c>, so both entry points validate identically.
/// </summary>
public class ReserveChangeRules : AbstractValidator<IReserveChangeRequest>
{
    public ReserveChangeRules()
    {
        RuleFor(x => x.Component).IsInEnum().WithMessage("Invalid reserve component type.");

        RuleFor(x => x.Amount)
            .Must((change, amount) => amount != 0 && (amount > 0 || change.Component == ReserveComponentType.SubrogationRecoverable))
            .WithMessage("Reserve amount must be greater than zero.");

        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(500);
    }
}
