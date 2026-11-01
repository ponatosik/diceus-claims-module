using ClaimsModule.Application;
using ClaimsModule.Application.Common;
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

        // FileMode.CreateNew (not .Create) — a defensive backstop against ever silently overwriting
        // an existing document's bytes if the caller's own uniqueness guarantee is ever violated.
        await using var fileStream = new FileStream(fullPath, FileMode.CreateNew);
        await content.CopyToAsync(fileStream, cancellationToken);

        return $"/uploads/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(blobPath);
}
