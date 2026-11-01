using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain;
using FluentValidation.Results;
using MediatR;

namespace ClaimsModule.Application.Claims.CreateClaim;

/// <summary>
/// FNOL intake (§10.1, US13). No initial-reserve field by design — this slice writes nothing to the
/// reserve tables (see the plan's Resolved Ambiguity 2); that's Slice C's job.
/// </summary>
public class CreateClaimCommand : IRequest<ClaimDto>, IHasValidationWarnings
{
    public Guid? PolicyId { get; set; }

    public DateTimeOffset LossDate { get; set; }

    public string LossDescription { get; set; } = string.Empty;

    public string? LossLocation { get; set; }

    public string CauseOfLossCode { get; set; } = string.Empty;

    public decimal? EstimatedLossAmount { get; set; }

    public List<CreatePartyDto> Parties { get; set; } = [];

    public List<CreateRiskObjectDto> RiskObjects { get; set; } = [];

    public List<ValidationFailure> Warnings { get; } = [];
}

public record CreatePartyDto(
    PartyRole PartyRole,
    PartyType PartyType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone);

public record CreateRiskObjectDto(
    AssetType AssetType,
    string AssetDescription,
    string? DamageDescription,
    bool IsPrimary,
    string? AssetReference);
