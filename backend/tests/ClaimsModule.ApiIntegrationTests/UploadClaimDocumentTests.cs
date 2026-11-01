using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class UploadClaimDocumentTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static async Task<HttpResponseMessage> UploadDocumentAsync(
        HttpClient client, Guid claimId, byte[] bytes, string contentType, string fileName,
        string documentType = "PoliceReport", string? notes = null)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "File", fileName);
        form.Add(new StringContent(documentType), "DocumentType");
        if (notes is not null)
        {
            form.Add(new StringContent(notes), "Notes");
        }

        return await client.PostAsync($"/api/claims/{claimId}/documents", form);
    }

    [Fact]
    public async Task Uploading_a_PDF_creates_the_document_and_a_DOCUMENT_UPLOADED_audit_entry()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await UploadDocumentAsync(client, claim.ClaimId, [1, 2, 3, 4], "application/pdf", "report.pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<ClaimDocumentDetailDto>();
        Assert.False(string.IsNullOrWhiteSpace(document!.DownloadUrl));

        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit");
        Assert.Single(audit!.Items, a => a.EventType == "DOCUMENT_UPLOADED");
    }

    [Fact]
    public async Task Uploading_a_disallowed_content_type_is_rejected()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await UploadDocumentAsync(client, claim.ClaimId, [1, 2, 3], "application/x-msdownload", "virus.exe");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("ContentType", body!.Errors!.Keys);
    }

    [Fact]
    public async Task Uploading_a_file_over_50MB_is_rejected()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        var oversized = new byte[50 * 1024 * 1024 + 1];

        var response = await UploadDocumentAsync(client, claim.ClaimId, oversized, "application/pdf", "huge.pdf");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("FileSizeBytes", body!.Errors!.Keys);
    }

    [Fact]
    public async Task Uploading_with_an_overly_long_FileName_is_rejected()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        var longFileName = new string('a', 250) + ".pdf"; // 254 chars — over the 200-char limit

        var response = await UploadDocumentAsync(client, claim.ClaimId, [1, 2, 3], "application/pdf", longFileName);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("FileName", body!.Errors!.Keys);
    }

    [Fact]
    public async Task Uploading_with_overly_long_Notes_is_rejected()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        var longNotes = new string('a', 501);

        var response = await UploadDocumentAsync(client, claim.ClaimId, [1, 2, 3], "application/pdf", "report.pdf", notes: longNotes);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Notes", body!.Errors!.Keys);
    }

    [Fact]
    public async Task Uploading_to_a_nonexistent_claim_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await UploadDocumentAsync(client, Guid.NewGuid(), [1, 2, 3], "application/pdf", "report.pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_path_traversal_filename_is_sanitised_before_reaching_storage()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await UploadDocumentAsync(client, claim.ClaimId, [1, 2, 3], "text/plain", "../../etc/passwd");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<ClaimDocumentDetailDto>();

        // Asserting "no .." in the URL alone proves nothing — Uri collapses dot segments on its own,
        // so an unsanitised "{org}/{claim}/../../etc/passwd" would also show no ".." once built into
        // a URI. Check the actual last path segment instead: it must be the bare, sanitised "passwd"
        // stem (plus the uniqueness suffix), never "etc" or anything containing a separator.
        var lastSegment = new Uri(document!.DownloadUrl).AbsolutePath.Split('/').Last();
        Assert.StartsWith("passwd", lastSegment);
        Assert.DoesNotContain("etc", document.DownloadUrl);
    }

    [Fact]
    public async Task Two_uploads_with_the_same_filename_do_not_collide()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        var firstBytes = new byte[] { 1, 1, 1 };
        var secondBytes = new byte[] { 2, 2, 2 };

        var firstResponse = await UploadDocumentAsync(client, claim.ClaimId, firstBytes, "text/plain", "notes.txt");
        var secondResponse = await UploadDocumentAsync(client, claim.ClaimId, secondBytes, "text/plain", "notes.txt");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<ClaimDocumentDetailDto>();
        var second = await secondResponse.Content.ReadFromJsonAsync<ClaimDocumentDetailDto>();

        using var plainClient = new HttpClient();
        Assert.Equal(firstBytes, await plainClient.GetByteArrayAsync(first!.DownloadUrl));
        Assert.Equal(secondBytes, await plainClient.GetByteArrayAsync(second!.DownloadUrl));
    }
}
