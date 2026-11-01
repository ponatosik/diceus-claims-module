using System.Text;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace ClaimsModule.UnitTests;

public class LocalFileSystemStorageServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"claims-module-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private LocalFileSystemStorageService CreateService() =>
        new(Options.Create(new LocalStorageOptions { RootPath = _tempRoot }));

    [Fact]
    public async Task Uploading_writes_the_file_under_organisationId_claimId_and_returns_an_uploads_prefixed_path()
    {
        var service = CreateService();
        var organisationId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("hello world");

        string blobPath;
        using (var stream = new MemoryStream(bytes))
        {
            blobPath = await service.UploadAsync(organisationId, claimId, "report.pdf", stream, "application/pdf");
        }

        var expectedFile = Path.Combine(_tempRoot, organisationId.ToString(), claimId.ToString(), "report.pdf");
        Assert.True(File.Exists(expectedFile));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(expectedFile));
        Assert.StartsWith("/uploads/", blobPath);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_returns_the_same_path_UploadAsync_returned()
    {
        var service = CreateService();
        string blobPath;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("content")))
        {
            blobPath = await service.UploadAsync(Guid.NewGuid(), Guid.NewGuid(), "notes.txt", stream, "text/plain");
        }

        var downloadUrl = await service.GetDownloadUrlAsync(blobPath);

        Assert.Equal(blobPath, downloadUrl);
    }

    [Fact]
    public async Task Uploading_to_an_existing_path_throws_instead_of_silently_overwriting()
    {
        var service = CreateService();
        var organisationId = Guid.NewGuid();
        var claimId = Guid.NewGuid();

        using (var firstStream = new MemoryStream(Encoding.UTF8.GetBytes("first")))
        {
            await service.UploadAsync(organisationId, claimId, "report.pdf", firstStream, "application/pdf");
        }

        using var secondStream = new MemoryStream(Encoding.UTF8.GetBytes("second"));
        await Assert.ThrowsAsync<IOException>(
            () => service.UploadAsync(organisationId, claimId, "report.pdf", secondStream, "application/pdf"));
    }
}
