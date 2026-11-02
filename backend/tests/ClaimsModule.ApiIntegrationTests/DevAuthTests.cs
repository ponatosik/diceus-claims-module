using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClaimsModule.API.Controllers;
using ClaimsModule.Persistence;
using Microsoft.Extensions.Configuration;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class DevAuthTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Mints_a_usable_token_for_a_demo_user()
    {
        using var client = Fixture.CreateClient();

        var tokenResponse = await client.PostAsJsonAsync("/api/dev/auth/token", new DevTokenRequest(SeedData.DemoSupervisorUserId));
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var body = (await tokenResponse.Content.ReadFromJsonAsync<DevTokenResponse>())!;

        Assert.Equal(SeedData.DemoSupervisorUserId, body.UserId);
        Assert.Equal("supervisor", body.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.DisplayName));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var protectedResponse = await client.GetAsync("/api/reference/claim-statuses");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task The_token_carries_the_demo_users_role_from_the_directory()
    {
        using var client = Fixture.CreateClient();

        var managerToken = (await (await client.PostAsJsonAsync("/api/dev/auth/token", new DevTokenRequest(SeedData.DemoManagerUserId)))
            .Content.ReadFromJsonAsync<DevTokenResponse>())!.AccessToken;

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(managerToken);
        Assert.Contains(jwt.Claims, c => c.Type.EndsWith("/role", StringComparison.Ordinal) && c.Value == "manager");
        Assert.Contains(jwt.Claims, c => c.Type.EndsWith("/nameidentifier", StringComparison.Ordinal) && c.Value == SeedData.DemoManagerUserId.ToString());
    }

    [Fact]
    public async Task Rejects_an_unknown_user_id()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/dev/auth/token", new DevTokenRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_when_dev_auth_is_disabled()
    {
        using var factory = Fixture.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["DevAuth:Enabled"] = "false" })));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/dev/auth/token", new DevTokenRequest(SeedData.DemoHandlerUserId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
