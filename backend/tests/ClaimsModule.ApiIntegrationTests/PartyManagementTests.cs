using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims.AddClaimParty;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class PartyManagementTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static CreateClaimCommand ValidCommand(List<CreatePartyDto>? parties = null) => new()
    {
        LossDate = DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = "A pipe burst and flooded the basement overnight.",
        CauseOfLossCode = "COL-FIRE",
        Parties = parties ?? [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
        RiskObjects = [],
    };

    private async Task<ClaimDto> CreateClaimAsync(HttpClient client, List<CreatePartyDto>? parties = null) =>
        (await (await client.PostAsJsonAsync("/api/claims", ValidCommand(parties))).Content.ReadFromJsonAsync<ClaimDto>())!;

    [Fact]
    public async Task Adding_a_party_succeeds_and_the_claim_now_shows_both()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/claims/{claim.ClaimId}/parties",
            new AddClaimPartyCommand { ClaimId = claim.ClaimId, PartyRole = PartyRole.Witness, PartyType = PartyType.Person, FirstName = "Sam", LastName = "Smith" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.Equal(2, detail!.Parties.Count);
    }

    [Fact]
    public async Task Removing_a_non_last_claimant_succeeds_and_the_other_remains_active()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client, parties:
        [
            new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null),
            new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "John", "Roe", null, null, null),
        ]);
        var firstClaimantId = claim.Parties.First(p => p.PartyRole == "Claimant").ClaimPartyId;

        var response = await client.DeleteAsync($"/api/claims/{claim.ClaimId}/parties/{firstClaimantId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.Single(detail!.Parties, p => p.IsActive && p.PartyRole == "Claimant");
    }

    [Fact]
    public async Task Removing_the_last_active_claimant_is_rejected_with_422_BR_P_01()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        var claimantId = claim.Parties.Single().ClaimPartyId;

        var response = await client.DeleteAsync($"/api/claims/{claim.ClaimId}/parties/{claimantId}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("BR-P-01", body!.BlockingConditions![0].Code);
    }

    [Fact]
    public async Task Adding_a_party_to_a_nonexistent_claim_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync(
            $"/api/claims/{Guid.NewGuid()}/parties",
            new AddClaimPartyCommand { PartyRole = PartyRole.Witness, PartyType = PartyType.Person, FirstName = "Sam", LastName = "Smith" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Removing_a_nonexistent_party_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await client.DeleteAsync($"/api/claims/{claim.ClaimId}/parties/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
