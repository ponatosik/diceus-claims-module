using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Policies;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class PolicyLookupTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Search_by_policy_number_and_by_client_name_return_the_same_single_result()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var byNumber = await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001");
        var byName = await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=Meridian");

        Assert.Single(byNumber!);
        Assert.Single(byName!);
        Assert.Equal(byNumber![0].PolicyId, byName![0].PolicyId);
        Assert.Equal("POL-2024-001001", byNumber[0].PolicyNumber);
    }

    [Fact]
    public async Task Coverage_for_unknown_policy_id_returns_404_NotFound()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync($"/api/policies/{Guid.NewGuid()}/coverage");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("NotFound", body!["type"].ToString());
    }

    [Fact]
    public async Task Coverage_for_a_known_policy_returns_its_coverage_types()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var searchResults = await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2025-002001");
        var policyId = searchResults!.Single().PolicyId;

        var response = await client.GetAsync($"/api/policies/{policyId}/coverage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PolicyCoverageDto>();
        Assert.Equal(["Property", "Equipment"], body!.CoverageTypes);
    }
}
