namespace ClaimsModule.Application.Claims.Dtos;

/// <summary>A user reference resolved against the Demo User directory; DisplayName is null for an id the directory doesn't know.</summary>
public record UserSummaryDto(Guid UserId, string? DisplayName);
