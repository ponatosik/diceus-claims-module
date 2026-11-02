namespace ClaimsModule.Application.Claims.Dtos;

/// <summary>
/// Response shape for the upload (Slice E Task 5) and list (Task 6) document endpoints — richer than
/// <see cref="ClaimDocumentDto"/>, which stays as Slice B's lightweight summary embedded in
/// <see cref="ClaimDetailDto"/>.
/// </summary>
public record ClaimDocumentDetailDto(
    Guid ClaimDocumentId,
    Guid ClaimId,
    string DocumentType,
    string DocumentName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset UploadedAt,
    Guid? UploadedByUserId,
    string? Notes,
    string DownloadUrl,
    string? UploadedByDisplayName);
