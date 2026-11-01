namespace ClaimsModule.Application;

/// <summary>
/// Provider-abstracted document storage (BR-D-03, §13) — <c>AzureBlobStorageService</c> is primary,
/// <c>LocalFileSystemStorageService</c> is the dev fallback, both in <c>ClaimsModule.Infrastructure</c>.
/// Callers are expected to sanitise <paramref name="sanitisedFileName"/> themselves before calling
/// <see cref="UploadAsync"/> — neither implementation re-sanitises, so sanitisation happens exactly once.
/// </summary>
public interface IStorageService
{
    /// <returns>A provider-specific path/identifier to persist as <c>ClaimDocument.BlobPath</c> and pass
    /// back into <see cref="GetDownloadUrlAsync"/> later.</returns>
    Task<string> UploadAsync(
        Guid organisationId, Guid claimId, string sanitisedFileName, Stream content, string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>A short-lived (1-hour TTL for the Azure case, BR-D-02) URL the caller can fetch directly.</summary>
    Task<string> GetDownloadUrlAsync(string blobPath, CancellationToken cancellationToken = default);
}
