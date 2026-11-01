using ClaimsModule.Domain;
using FluentValidation;

namespace ClaimsModule.Application.Claims.CreateClaim;

/// <summary>
/// §8's validation rules for FNOL intake. Per the plan's Resolved Ambiguity 1, every rule here is
/// Severity.Error (default) unless explicitly marked Warning — Errors block creation outright via
/// ValidationBehavior; Warnings let the claim persist into Draft and get audited as
/// VALIDATION_ISSUE_ADDED by the handler.
/// </summary>
public class CreateClaimCommandValidator : AbstractValidator<CreateClaimCommand>
{
    public CreateClaimCommandValidator(ICauseOfLossCodeRepository causeOfLossCodeRepository, IPolicyRepository policyRepository)
    {
        RuleFor(x => x.LossDate)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("Loss date is required.");

        RuleFor(x => x.LossDate)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow)
            .WithMessage("Loss date cannot be in the future.");

        RuleFor(x => x.LossDescription)
            .Must(description => !string.IsNullOrWhiteSpace(description) && description.Length >= 20)
            .WithMessage("Loss description is required and must be at least 20 characters.");

        RuleFor(x => x.CauseOfLossCode)
            .MustAsync(async (code, cancellationToken) =>
            {
                var reference = await causeOfLossCodeRepository.GetByCodeAsync(code, cancellationToken);
                return reference is { IsActive: true };
            })
            .WithMessage("Cause of loss code is not recognised or is inactive.");

        RuleFor(x => x.Parties)
            .Must(parties => parties.Any(p => p.PartyRole == PartyRole.Claimant))
            .WithMessage("At least one Claimant party is required to open a claim.");

        // BR-C-06: missing policy is a Warning, not a block.
        RuleFor(x => x.PolicyId)
            .NotNull()
            .WithSeverity(Severity.Warning)
            .WithMessage("No policy linked. Policy must be associated before reserves can be set.");

        RuleFor(x => x.PolicyId)
            .MustAsync(async (policyId, cancellationToken) =>
                await policyRepository.GetByIdAsync(policyId!.Value, cancellationToken) is not null)
            .When(x => x.PolicyId is not null)
            .WithSeverity(Severity.Warning)
            .WithMessage("Linked policy could not be found.");

        // BR-C-02: loss date outside the linked policy's effective period is a Warning, not a block.
        RuleFor(x => x)
            .CustomAsync(async (command, context, cancellationToken) =>
            {
                if (command.PolicyId is not { } policyId)
                {
                    return;
                }

                var policy = await policyRepository.GetByIdAsync(policyId, cancellationToken);
                if (policy is null)
                {
                    return;
                }

                var lossDate = DateOnly.FromDateTime(command.LossDate.UtcDateTime);
                if (lossDate < policy.EffectiveDate || lossDate > policy.ExpirationDate)
                {
                    context.AddFailure(new FluentValidation.Results.ValidationFailure(
                        nameof(CreateClaimCommand.LossDate), "Loss date is outside the policy effective period.")
                    {
                        Severity = Severity.Warning,
                    });
                }
            });

        // §5.4: no risk objects linked is a Warning, not a block.
        RuleFor(x => x.RiskObjects)
            .Must(riskObjects => riskObjects.Count > 0)
            .WithSeverity(Severity.Warning)
            .WithMessage("No risk objects linked to this claim.");

        RuleForEach(x => x.Parties).ChildRules(party =>
        {
            party.RuleFor(p => p.FirstName).NotEmpty().When(p => p.PartyType == PartyType.Person);
            party.RuleFor(p => p.LastName).NotEmpty().When(p => p.PartyType == PartyType.Person);
            party.RuleFor(p => p.CompanyName).NotEmpty().When(p => p.PartyType == PartyType.Company);
        });

        RuleForEach(x => x.RiskObjects).ChildRules(riskObject =>
        {
            riskObject.RuleFor(r => r.AssetDescription).NotEmpty();
        });
    }
}
