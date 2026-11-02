using ClaimsModule.Domain.Enums;
using FluentValidation;

namespace ClaimsModule.Application.Claims;

/// <summary>The fields every reserve change carries, whether submitted on its own or as FNOL's initial reserve.</summary>
public interface IReserveChangeRequest
{
    ReserveComponentType Component { get; }

    decimal Amount { get; }

    string ChangeReason { get; }

    /// <summary>Whether the client submitted an Add — which must be positive (BR-R-01). Whether it actually opens a component is the server's call (ReserveBalanceRules).</summary>
    bool IsAddTransaction { get; }
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
            .When(x => x.IsAddTransaction)
            .WithMessage("Reserve amount must be greater than zero.");

        RuleFor(x => x.Amount)
            .NotEqual(0m)
            .When(x => !x.IsAddTransaction)
            .WithMessage("Reserve amount must not be zero.");

        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(500);
    }
}
