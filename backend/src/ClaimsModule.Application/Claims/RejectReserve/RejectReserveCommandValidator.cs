using FluentValidation;

namespace ClaimsModule.Application.Claims.RejectReserve;

/// <summary>§6.4: rejecting a pending reserve records a rejection reason.</summary>
public class RejectReserveCommandValidator : AbstractValidator<RejectReserveCommand>
{
    public RejectReserveCommandValidator()
    {
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500);
    }
}
