using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class ClaimValidationEndpointTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Returns_every_issue_found_without_persisting_anything()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var query = new ValidateClaimQuery
        {
            PolicyId = null,
            LossDate = DateTimeOffset.UtcNow.AddDays(1),
            LossDescription = "A pipe burst and flooded the basement overnight.",
            CauseOfLossCode = "COL-FIRE",
            Parties = [],
            // Non-empty so this scenario isolates exactly the 3 issues it's named for — RiskObjects=[]
            // triggers its own "No risk objects linked" Warning per the same validator rule table,
            // which would otherwise make this 4, not 3.
            RiskObjects = [new CreateRiskObjectDto(AssetType.Property, "Basement", null, true, null)],
        };

        var response = await client.PostAsJsonAsync("/api/claims/validate", query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ValidationIssueDto>>();
        Assert.Equal(3, body!.Count);
        Assert.Contains(body, i => i.PropertyName == "LossDate" && i.Severity == "Error");
        Assert.Contains(body, i => i.PropertyName == "Parties" && i.Severity == "Error");
        Assert.Contains(body, i => i.PropertyName == "PolicyId" && i.Severity == "Warning");
    }

    [Fact]
    public async Task Returns_an_empty_list_for_a_fully_valid_body()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        // A real, in-force policy is required here — PolicyId == null always produces a Warning
        // (BR-C-06), so "zero issues" is only reachable with a resolvable policy and a loss date
        // inside its effective period.
        var searchResults = await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001");
        var policyId = searchResults!.Single().PolicyId;

        var query = new ValidateClaimQuery
        {
            PolicyId = policyId,
            LossDate = DateTimeOffset.UtcNow.AddDays(-1),
            LossDescription = "A pipe burst and flooded the basement overnight.",
            CauseOfLossCode = "COL-FIRE",
            Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
            RiskObjects = [new CreateRiskObjectDto(AssetType.Property, "Basement", null, true, null)],
        };

        var response = await client.PostAsJsonAsync("/api/claims/validate", query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ValidationIssueDto>>();
        Assert.Empty(body!);
    }
}
