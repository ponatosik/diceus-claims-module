using FluentValidation;

namespace ClaimsModule.Application.Claims.UpdateClaimNotes;

public class UpdateClaimNotesCommandValidator : AbstractValidator<UpdateClaimNotesCommand>
{
    public const int MaxNotesLength = 4000;

    public UpdateClaimNotesCommandValidator()
    {
        // The column is NVARCHAR(MAX); the cap is a sanity bound on a free-text UI field, not a storage limit.
        RuleFor(x => x.Notes).MaximumLength(MaxNotesLength);
    }
}
