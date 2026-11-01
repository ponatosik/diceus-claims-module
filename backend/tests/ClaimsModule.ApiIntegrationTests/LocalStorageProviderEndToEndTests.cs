using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>
/// Proves the LocalFileSystem provider (BR-D-03) end-to-end over real HTTP — upload, list, and fetch
/// the returned local URL via the static-file middleware Program.cs wires. Everything else in this
/// slice exercises the Azure/Azurite path exclusively; this is the one seam that needed its own host
/// instance with StorageProvider overridden (<see cref="ApiFixture.CreateAuthenticatedClientWithStorageProvider"/>).
/// </summary>
[Collection(ApiCollection.Name)]
public class LocalStorageProviderEndToEndTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<ClaimDto> CreateClaimAsync(HttpClient client)
    {
        var policyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        var command = new CreateClaimCommand
        {
            PolicyId = policyId,
            LossDate = DateTimeOffset.UtcNow.AddDays(-1),
            LossDescription = "A pipe burst and flooded the basement overnight.",
            CauseOfLossCode = "COL-FIRE",
            Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
            RiskObjects = [],
        };
        return (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    private static async Task<HttpResponseMessage> UploadDocumentAsync(HttpClient client, Guid claimId, byte[] bytes, string contentType, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "File", fileName);
        form.Add(new StringContent("PoliceReport"), "DocumentType");

        return await client.PostAsync($"/api/claims/{claimId}/documents", form);
    }

    [Fact]
    public async Task Uploading_and_fetching_via_the_static_file_middleware_works_even_for_an_extensionless_filename()
    {
        using var client = Fixture.CreateAuthenticatedClientWithStorageProvider("LocalFileSystem", "handler");
        var claim = await CreateClaimAsync(client);
        var bytes = new byte[] { 9, 8, 7, 6 };

        // "../../etc/passwd" sanitises down to the extensionless "passwd" — exactly the shape that
        // used to 404 against the static-file middleware before ServeUnknownFileTypes was set.
        var uploadResponse = await UploadDocumentAsync(client, claim.ClaimId, bytes, "text/plain", "../../etc/passwd");
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var document = await uploadResponse.Content.ReadFromJsonAsync<ClaimDocumentDetailDto>();
        Assert.StartsWith("/uploads/", document!.DownloadUrl);

        var downloaded = await client.GetByteArrayAsync(document.DownloadUrl);
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task The_static_file_response_is_hardened_against_being_rendered_as_HTML()
    {
        using var client = Fixture.CreateAuthenticatedClientWithStorageProvider("LocalFileSystem", "handler");
        var claim = await CreateClaimAsync(client);

        var uploadResponse = await UploadDocumentAsync(client, claim.ClaimId, [1, 2, 3], "text/plain", "notes.html");
        var document = await uploadResponse.Content.ReadFromJsonAsync<ClaimDocumentDetailDto>();

        var downloadResponse = await client.GetAsync(document!.DownloadUrl);

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("nosniff", downloadResponse.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("attachment", downloadResponse.Content.Headers.ContentDisposition!.DispositionType);
    }
}
