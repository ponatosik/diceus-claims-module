using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class ReferenceDataTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Lists_all_ten_seeded_cause_of_loss_codes()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync("/api/reference/cause-of-loss-codes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        Assert.Equal(10, body!.Count);
    }

    [Fact]
    public async Task Filters_by_peril_category()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync("/api/reference/cause-of-loss-codes?perilCategory=Weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>();
        Assert.Equal(["COL-FLOOD", "COL-WIND"], body!.Select(c => c.Code));
    }
}
