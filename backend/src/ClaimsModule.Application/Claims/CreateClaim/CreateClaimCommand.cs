using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using FluentValidation.Results;
using MediatR;

namespace ClaimsModule.Application.Claims.CreateClaim;

/// <summary>
/// FNOL intake (§10.1, US13), with §11.2 Step 3's optional initial reserve (spec 006 story 7) recorded
/// in the same transaction as the claim.
/// </summary>
public class CreateClaimCommand : IRequest<ClaimDto>, IHasValidationWarnings
{
    public Guid? PolicyId { get; set; }

    public DateTimeOffset LossDate { get; set; }

    public string LossDescription { get; set; } = string.Empty;

    public string? LossLocation { get; set; }

    public string CauseOfLossCode { get; set; } = string.Empty;

    public decimal? EstimatedLossAmount { get; set; }

    // Nullable (not just default-initialized) so an explicit JSON "parties": null doesn't trip
    // [ApiController]'s implicit "non-nullable reference type => required" ModelState check, which
    // would otherwise short-circuit to a 400 with the framework's default body shape instead of
    // reaching FluentValidation's standard 422 errors shape (FR-ERR-01).
    public List<CreatePartyDto>? Parties { get; set; } = [];

    public List<CreateRiskObjectDto>? RiskObjects { get; set; } = [];

    /// <summary>Optional. Always recorded as an Add transaction on a brand-new component.</summary>
    public CreateInitialReserveDto? InitialReserve { get; set; }

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

public record CreateInitialReserveDto(
    ReserveComponentType Component,
    decimal Amount,
    string ChangeReason) : IReserveChangeRequest
{
    /// <summary>FNOL's initial reserve always opens its component.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAddTransaction => true;
}
