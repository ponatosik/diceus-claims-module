namespace ClaimsModule.Application.Claims.Dtos;

public record ClaimDocumentDto(Guid ClaimDocumentId, string DocumentName, string DocumentType, DateTimeOffset UploadedAt);
