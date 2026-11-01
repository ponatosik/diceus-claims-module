using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthenticationTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Request_without_token_is_rejected()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync("/api/reference/claim-statuses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_valid_token_succeeds_and_returns_transition_table()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync("/api/reference/claim-statuses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ClaimStatusDto>>();
        Assert.Contains(body!, s => s.Status == "Closed" && s.ValidNextStatuses.Contains("Reopened"));
    }
}
