using System.Text;
using ClaimsModule.Application;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class AzureBlobStorageServiceTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Uploading_then_generating_a_download_URL_round_trips_the_exact_bytes()
    {
        using var scope = Fixture.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var bytes = Encoding.UTF8.GetBytes("hello from AzureBlobStorageServiceTests");

        string blobPath;
        using (var stream = new MemoryStream(bytes))
        {
            blobPath = await storageService.UploadAsync(Guid.NewGuid(), Guid.NewGuid(), "notes.txt", stream, "text/plain");
        }

        var downloadUrl = await storageService.GetDownloadUrlAsync(blobPath);

        using var plainClient = new HttpClient();
        var downloaded = await plainClient.GetByteArrayAsync(downloadUrl);
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task The_generated_SAS_URL_expires_in_about_one_hour()
    {
        using var scope = Fixture.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var blobPath = await storageService.UploadAsync(Guid.NewGuid(), Guid.NewGuid(), "doc.pdf", stream, "application/pdf");

        var downloadUrl = await storageService.GetDownloadUrlAsync(blobPath);

        var query = QueryHelpers.ParseQuery(new Uri(downloadUrl).Query);
        var expiry = DateTimeOffset.Parse(query["se"]!);
        Assert.InRange(expiry, DateTimeOffset.UtcNow.AddMinutes(55), DateTimeOffset.UtcNow.AddMinutes(65));
    }
}
