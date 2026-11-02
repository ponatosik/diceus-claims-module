using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// POST /api/claims/validate — the "tell me what's wrong" surface the FNOL form uses per-step (§5.4/ADR-0008).
/// Shares CreateClaimCommandValidator's rules exactly; never throws, never persists, always 200.
/// </summary>
public class ValidateClaimQuery : IRequest<List<ValidationIssueDto>>
{
    public Guid? PolicyId { get; set; }

    public DateTimeOffset LossDate { get; set; }

    public string LossDescription { get; set; } = string.Empty;

    public string? LossLocation { get; set; }

    public string CauseOfLossCode { get; set; } = string.Empty;

    public decimal? EstimatedLossAmount { get; set; }

    public List<CreatePartyDto> Parties { get; set; } = [];

    public List<CreateRiskObjectDto> RiskObjects { get; set; } = [];

    public CreateInitialReserveDto? InitialReserve { get; set; }
}

public class ValidateClaimQueryHandler(IValidator<CreateClaimCommand> validator)
    : IRequestHandler<ValidateClaimQuery, List<ValidationIssueDto>>
{
    public async Task<List<ValidationIssueDto>> Handle(ValidateClaimQuery request, CancellationToken cancellationToken)
    {
        var transientCommand = new CreateClaimCommand
        {
            PolicyId = request.PolicyId,
            LossDate = request.LossDate,
            LossDescription = request.LossDescription,
            LossLocation = request.LossLocation,
            CauseOfLossCode = request.CauseOfLossCode,
            EstimatedLossAmount = request.EstimatedLossAmount,
            Parties = request.Parties,
            RiskObjects = request.RiskObjects,
            InitialReserve = request.InitialReserve,
        };

        var result = await validator.ValidateAsync(transientCommand, cancellationToken);

        return [.. result.Errors.Select(failure => new ValidationIssueDto(failure.PropertyName, failure.ErrorMessage, failure.Severity.ToString()))];
    }
}
