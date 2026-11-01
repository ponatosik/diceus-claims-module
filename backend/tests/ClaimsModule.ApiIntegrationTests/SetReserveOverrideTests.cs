using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class SetReserveOverrideTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<ClaimDto> CreateClaimWithPolicyAsync(HttpClient client)
    {
        var policyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        var command = new CreateClaimCommand
        {
            PolicyId = policyId,
            LossDate = DateTimeOffset.UtcNow.AddDays(-1),
            LossDescription = "A pipe burst and flooded the basement overnight.",
            CauseOfLossCode = "COL-FIRE",
            Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
            RiskObjects = [],
        };
        return (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    private static Task<HttpResponseMessage> SetOverrideAsync(HttpClient client, Guid claimId) =>
        client.PostAsync($"/api/claims/{claimId}/reserves/override", null);

    [Fact]
    public async Task A_manager_can_set_the_override_flag()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);

        using var managerClient = Fixture.CreateAuthenticatedClient("manager");
        var response = await SetOverrideAsync(managerClient, claim.ClaimId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var detail = await handlerClient.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.True(detail!.ReserveOverrideApproved);
    }

    [Theory]
    [InlineData("handler")]
    [InlineData("supervisor")]
    public async Task A_non_manager_cannot_set_the_override_flag(string role)
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);

        using var client = Fixture.CreateAuthenticatedClient(role);
        var response = await SetOverrideAsync(client, claim.ClaimId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("BR-R-05", body!.BlockingConditions![0].Code);
    }
}
