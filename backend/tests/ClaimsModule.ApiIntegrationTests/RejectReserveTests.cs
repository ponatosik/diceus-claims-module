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
public class RejectReserveTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<ClaimDto> CreateClaimWithPolicyAsync(HttpClient client)
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

    private static Task<HttpResponseMessage> RejectReserveAsync(HttpClient client, Guid claimId, Guid reserveHistoryId, string reason) =>
        client.PostAsJsonAsync($"/api/claims/{claimId}/reserves/{reserveHistoryId}/reject", new { RejectionReason = reason });

    [Fact]
    public async Task Happy_path_by_supervisor_rejects_without_moving_the_balance()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);

        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var response = await RejectReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId, "Amount not supported by documentation.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReserveTransactionDto>();
        Assert.Equal("Rejected", body!.ApprovalStatus);

        var detail = await handlerClient.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.Equal(0m, detail!.Reserves.Single().CurrentAmount);
    }

    [Fact]
    public async Task A_handler_cannot_reject()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);

        var response = await RejectReserveAsync(client, claim.ClaimId, opened.ReserveHistoryId, "Not supported.");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("BR-R-02", body!.BlockingConditions![0].Code);
    }

    [Fact]
    public async Task Rejecting_an_already_rejected_transaction_is_rejected_as_not_pending()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);
        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        await RejectReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId, "Not supported.");

        var secondAttempt = await RejectReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId, "Still not supported.");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondAttempt.StatusCode);
        var body = await secondAttempt.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("RESERVE-NOT-PENDING-01", body!.BlockingConditions![0].Code);
    }

    [Fact]
    public async Task An_empty_rejection_reason_is_rejected()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);
        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");

        var response = await RejectReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId, "");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Resubmitting_after_rejection_creates_a_new_history_row_and_keeps_the_rejected_one()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);
        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        await RejectReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId, "Not supported.");

        var resubmitted = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 40_000m);

        Assert.NotEqual(opened.ReserveHistoryId, resubmitted.ReserveHistoryId);
        var list = await handlerClient.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves");
        Assert.Contains(list!.History, h => h.ReserveHistoryId == opened.ReserveHistoryId && h.ApprovalStatus == "Rejected");
        Assert.Contains(list.History, h => h.ReserveHistoryId == resubmitted.ReserveHistoryId && h.ApprovalStatus == "PendingApproval");
    }
}
