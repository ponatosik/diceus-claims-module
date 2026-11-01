using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ClaimsModule.Application;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Primary <see cref="IStorageService"/> implementation (BR-D-01/BR-D-02, §13) — Azure Blob Storage,
/// container <c>claim-documents</c>, blob name <c>{organisationId}/{claimId}/{fileName}</c>, with a
/// 1-hour-TTL SAS URL for retrieval.
/// </summary>
public class AzureBlobStorageService(BlobServiceClient blobServiceClient) : IStorageService
{
    private const string ContainerName = "claim-documents";

    public async Task<string> UploadAsync(
        Guid organisationId, Guid claimId, string sanitisedFileName, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"{organisationId}/{claimId}/{sanitisedFileName}";
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(
            content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);

        return $"{ContainerName}/{blobName}";
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var firstSlash = blobPath.IndexOf('/');
        var containerName = blobPath[..firstSlash];
        var blobName = blobPath[(firstSlash + 1)..];

        var blobClient = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }
}
