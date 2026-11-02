using Microsoft.Extensions.Configuration;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class CorsTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string AllowedOrigin = "https://claims-frontend.example";

    private HttpClient CreateClientWithAllowedOrigin()
    {
        var factory = Fixture.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin })));
        return factory.CreateClient();
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/claims");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    [Fact]
    public async Task A_configured_origin_passes_preflight()
    {
        using var client = CreateClientWithAllowedOrigin();

        var response = await client.SendAsync(Preflight(AllowedOrigin));

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal(AllowedOrigin, Assert.Single(values));
    }

    [Fact]
    public async Task An_unconfigured_origin_gets_no_cors_headers()
    {
        using var client = CreateClientWithAllowedOrigin();

        var response = await client.SendAsync(Preflight("https://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
