using ClaimsModule.Application;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Dev fallback for <see cref="IStorageService"/> (BR-D-03, §13) — writes to the local filesystem
/// under <see cref="LocalStorageOptions.RootPath"/>, served back by <c>Program.cs</c>'s static-file
/// middleware mapped at <c>/uploads</c>. The returned path IS the servable URL, so
/// <see cref="GetDownloadUrlAsync"/> is a no-op.
/// </summary>
public class LocalFileSystemStorageService(IOptions<LocalStorageOptions> options) : IStorageService
{
    public async Task<string> UploadAsync(
        Guid organisationId, Guid claimId, string sanitisedFileName, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var relativePath = Path.Combine(organisationId.ToString(), claimId.ToString(), sanitisedFileName);
        var fullPath = Path.Combine(options.Value.RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return $"/uploads/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(blobPath);
}
