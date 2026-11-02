using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.UpdateClaimNotes;
using ClaimsModule.Persistence;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>Spec 006 story 9: §11.3 Overview's editable claim notes field.</summary>
[Collection(ApiCollection.Name)]
public class ClaimNotesTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<ClaimDto> CreateClaimAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/claims", ClaimDetailHeaderTests.ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>())!;

    [Fact]
    public async Task Updating_notes_persists_them_and_audits_old_and_new_values()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        var claim = await CreateClaimAsync(client);
        await client.PatchAsJsonAsync($"/api/claims/{claim.ClaimId}/notes", new UpdateClaimNotesCommand { Notes = "First call made." });

        var response = await client.PatchAsJsonAsync($"/api/claims/{claim.ClaimId}/notes", new UpdateClaimNotesCommand { Notes = "Adjuster visit booked." });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var detail = (await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}"))!;
        Assert.Equal("Adjuster visit booked.", detail.Notes);

        var audit = (await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit"))!;
        Assert.Equal(2, audit.Items.Count(e => e.EventType == "CLAIM_NOTES_UPDATED"));
    }

    [Fact]
    public async Task Notes_longer_than_4000_characters_are_rejected_with_422()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await client.PatchAsJsonAsync($"/api/claims/{claim.ClaimId}/notes", new UpdateClaimNotesCommand { Notes = new string('x', 4001) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Updating_notes_on_an_unknown_claim_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PatchAsJsonAsync($"/api/claims/{Guid.NewGuid()}/notes", new UpdateClaimNotesCommand { Notes = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
