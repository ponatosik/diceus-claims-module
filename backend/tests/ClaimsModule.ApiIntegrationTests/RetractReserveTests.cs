using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class RetractReserveTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static async Task<ReserveTransactionDto> OpenReserveAsync(
        HttpClient client, Guid claimId, ReserveComponentType component, decimal amount)
    {
        var response = await client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new OpenReserveCommand
        {
            Component = component,
            Amount = amount,
            ChangeReason = "Initial estimate",
            TransactionType = ReserveTransactionType.Add,
        });
        return (await response.Content.ReadFromJsonAsync<ReserveTransactionDto>())!;
    }

    private static Task<HttpResponseMessage> RetractReserveAsync(HttpClient client, Guid claimId, Guid reserveHistoryId) =>
        client.PostAsync($"/api/claims/{claimId}/reserves/{reserveHistoryId}/retract", null);

    [Fact]
    public async Task The_original_submitter_can_retract_their_own_pending_reserve()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);

        var response = await RetractReserveAsync(client, claim.ClaimId, opened.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReserveTransactionDto>();
        Assert.Equal("Cancelled", body!.ApprovalStatus);
    }

    [Fact]
    public async Task A_different_user_even_a_supervisor_cannot_retract_someone_elses_reserve()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);

        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var response = await RetractReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("RESERVE-NOT-PENDING-01", body!.BlockingConditions![0].Code);
    }

    [Fact]
    public async Task Retracting_an_already_approved_transaction_is_rejected()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);
        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        await supervisorClient.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{opened.ReserveHistoryId}/approve", null);

        var response = await RetractReserveAsync(handlerClient, claim.ClaimId, opened.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
