using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using FluentValidation;

namespace ClaimsModule.Application.Claims.AddClaimParty;

/// <summary>Same basic Person/Company shape checks as CreateClaimCommandValidator's nested party rules — no BR-* id, just field-shape validation.</summary>
public class AddClaimPartyCommandValidator : AbstractValidator<AddClaimPartyCommand>
{
    public AddClaimPartyCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().When(x => x.PartyType == PartyType.Person);
        RuleFor(x => x.LastName).NotEmpty().When(x => x.PartyType == PartyType.Person);
        RuleFor(x => x.CompanyName).NotEmpty().When(x => x.PartyType == PartyType.Company);
    }
}
