namespace ClaimsModule.Application.Claims.Dtos;

public record ClaimPartyDto(
    Guid ClaimPartyId,
    string PartyRole,
    string PartyType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    bool IsActive);
