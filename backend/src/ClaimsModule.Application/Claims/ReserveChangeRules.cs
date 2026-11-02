using ClaimsModule.Domain.Enums;
using FluentValidation;

namespace ClaimsModule.Application.Claims;

/// <summary>The fields every reserve change carries, whether submitted on its own or as FNOL's initial reserve.</summary>
public interface IReserveChangeRequest
{
    ReserveComponentType Component { get; }

    decimal Amount { get; }

    string ChangeReason { get; }

    /// <summary>Whether this change opens a component (an Add) — BR-R-01's positivity rule applies only then (ADR-0011).</summary>
    bool IsOpening { get; }
}

/// <summary>
/// §8 field rules for a reserve change — included by <c>OpenReserveCommandValidator</c> and applied to
/// <c>CreateClaimCommand.InitialReserve</c>, so both entry points validate identically. Per ADR-0011,
/// BR-R-01's "greater than zero" governs the opening amount; any later change is a signed, non-zero
/// delta whose effect on the balance is checked by <see cref="ReserveBalanceRules"/>.
/// </summary>
public class ReserveChangeRules : AbstractValidator<IReserveChangeRequest>
{
    public ReserveChangeRules()
    {
        RuleFor(x => x.Component).IsInEnum().WithMessage("Invalid reserve component type.");

        RuleFor(x => x.Amount)
            .Must((change, amount) => amount > 0 || (amount != 0 && change.Component == ReserveComponentType.SubrogationRecoverable))
            .When(x => x.IsOpening)
            .WithMessage("Reserve amount must be greater than zero.");

        RuleFor(x => x.Amount)
            .NotEqual(0m)
            .When(x => !x.IsOpening)
            .WithMessage("Reserve amount must not be zero.");

        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(500);
    }
}
