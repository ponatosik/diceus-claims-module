using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Controllers;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class DevAuthTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Mints_a_usable_token_for_a_valid_role()
    {
        using var client = Fixture.CreateClient();

        var tokenResponse = await client.PostAsJsonAsync("/api/dev/auth/token", new DevTokenRequest("handler"));
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = (await tokenResponse.Content.ReadFromJsonAsync<DevTokenResponse>())!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var protectedResponse = await client.GetAsync("/api/reference/claim-statuses");

        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Rejects_an_unrecognised_role()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/dev/auth/token", new DevTokenRequest("superuser"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
