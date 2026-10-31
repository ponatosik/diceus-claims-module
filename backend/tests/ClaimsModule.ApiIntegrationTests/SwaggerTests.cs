using System.Net;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class SwaggerTests(ApiFixture fixture)
{
    [Fact]
    public async Task Swagger_document_is_reachable()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
