using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class ListClaimDocumentsTests(ApiFixture fixture) : ApiTestBase(fixture)
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
    public async Task Listing_documents_for_a_claim_with_none_returns_an_empty_list()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await client.GetAsync($"/api/claims/{claim.ClaimId}/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documents = await response.Content.ReadFromJsonAsync<List<ClaimDocumentDetailDto>>();
        Assert.Empty(documents!);
    }

    [Fact]
    public async Task Listing_after_an_upload_returns_a_working_download_URL()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        var bytes = new byte[] { 10, 20, 30, 40, 50 };
        await UploadDocumentAsync(client, claim.ClaimId, bytes, "application/pdf", "report.pdf");

        var response = await client.GetAsync($"/api/claims/{claim.ClaimId}/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documents = await response.Content.ReadFromJsonAsync<List<ClaimDocumentDetailDto>>();
        var document = Assert.Single(documents!);

        using var plainClient = new HttpClient();
        var downloaded = await plainClient.GetByteArrayAsync(document.DownloadUrl);
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task Listing_documents_for_a_nonexistent_claim_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync($"/api/claims/{Guid.NewGuid()}/documents");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
