namespace ClaimsModule.Application.Claims.Dtos;

public record ClaimRiskObjectDto(
    Guid ClaimRiskObjectId,
    string AssetType,
    string AssetDescription,
    string? DamageDescription,
    bool IsPrimary,
    string? AssetReference);
