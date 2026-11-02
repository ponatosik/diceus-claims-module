using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class ListClaimReservesTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static Task<HttpResponseMessage> OpenReserveAsync(HttpClient client, Guid claimId, ReserveComponentType component, decimal amount) =>
        client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new OpenReserveCommand
        {
            Component = component,
            Amount = amount,
            ChangeReason = "Initial estimate",
            TransactionType = ReserveTransactionType.Add,
        });

    [Fact]
    public async Task Returns_component_balances_and_full_history_with_only_the_auto_approved_one_moving_the_balance()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Expense, 50_000m);

        var result = await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves");

        Assert.Equal(2, result!.Components.Count);
        Assert.Equal(5_000m, result.Components.Single(c => c.Component == "Indemnity").CurrentAmount);
        Assert.Equal(0m, result.Components.Single(c => c.Component == "Expense").CurrentAmount);

        Assert.Equal(2, result.History.Count);
        Assert.Equal(result.History.OrderByDescending(h => h.CreatedAt).Select(h => h.ReserveHistoryId), result.History.Select(h => h.ReserveHistoryId));
        Assert.Contains(result.History, h => h.Component == "Indemnity" && h.ApprovalStatus == "AutoApproved");
        Assert.Contains(result.History, h => h.Component == "Expense" && h.ApprovalStatus == "PendingApproval");
    }

    [Fact]
    public async Task Pending_amount_sums_only_the_components_transactions_still_awaiting_approval()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Expense, 5_000m);   // auto-approved — balance, not pending
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Expense, 50_000m);  // pending
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Expense, 20_000m);  // pending
        var retracted = (await (await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Expense, 30_000m))
            .Content.ReadFromJsonAsync<ReserveTransactionDto>())!;
        await client.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{retracted.ReserveHistoryId}/retract", null);

        var listed = (await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves"))!.Components.Single();
        var detail = (await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}"))!.Reserves.Single();

        Assert.Equal(5_000m, listed.CurrentAmount);
        Assert.Equal(70_000m, listed.PendingAmount);
        Assert.Equal(70_000m, detail.PendingAmount);
    }

    [Fact]
    public async Task Listing_reserves_for_an_unknown_claim_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync($"/api/claims/{Guid.NewGuid()}/reserves");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
