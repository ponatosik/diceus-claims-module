using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>
/// Reserve decreases and reversals (ADR-0011). BR-R-01's "greater than zero" governs the amount that
/// opens a component (Add); Adjust is a signed non-zero delta and Reverse zeroes the balance — neither
/// may take a component's balance below zero unless it is SubrogationRecoverable (§6.2 "May Go Negative?").
/// </summary>
[Collection(ApiCollection.Name)]
public class ReserveAdjustmentTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<ClaimDto> CreateClaimWithPolicyAsync(HttpClient client)
    {
        var command = ClaimDetailHeaderTests.ValidCommand();
        command.PolicyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        return (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    private static Task<HttpResponseMessage> SubmitAsync(
        HttpClient client, Guid claimId, ReserveComponentType component, decimal amount, ReserveTransactionType type) =>
        client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new OpenReserveCommand
        {
            Component = component,
            Amount = amount,
            ChangeReason = "Revised estimate",
            TransactionType = type,
        });

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid claimId, string component) =>
        (await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claimId}/reserves"))!.Components.Single(c => c.Component == component).CurrentAmount;

    private static async Task<string> BlockingCodeAsync(HttpResponseMessage response) =>
        Assert.Single((await response.Content.ReadFromJsonAsync<ErrorResponse>())!.BlockingConditions!).Code;

    [Fact]
    public async Task An_adjustment_can_decrease_a_reserve()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 8_000m, ReserveTransactionType.Add);

        var response = await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, -3_000m, ReserveTransactionType.Adjust);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(5_000m, await BalanceAsync(client, claim.ClaimId, "Indemnity"));
    }

    [Fact]
    public async Task An_adjustment_may_not_take_a_non_subrogation_balance_below_zero()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Expense, 2_000m, ReserveTransactionType.Add);

        var response = await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Expense, -2_500m, ReserveTransactionType.Adjust);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("RESERVE-BALANCE-NEGATIVE-01", await BlockingCodeAsync(response));
        Assert.Equal(2_000m, await BalanceAsync(client, claim.ClaimId, "Expense"));
    }

    [Fact]
    public async Task A_subrogation_recoverable_balance_may_go_negative()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await SubmitAsync(client, claim.ClaimId, ReserveComponentType.SubrogationRecoverable, -1_000m, ReserveTransactionType.Add);

        var response = await SubmitAsync(client, claim.ClaimId, ReserveComponentType.SubrogationRecoverable, -2_000m, ReserveTransactionType.Adjust);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(-3_000m, await BalanceAsync(client, claim.ClaimId, "SubrogationRecoverable"));
    }

    [Fact]
    public async Task A_zero_adjustment_is_rejected()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 1_000m, ReserveTransactionType.Add);

        var response = await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 0m, ReserveTransactionType.Adjust);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_reversal_zeroes_the_balance()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 6_000m, ReserveTransactionType.Add);

        var response = await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, -6_000m, ReserveTransactionType.Reverse);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0m, await BalanceAsync(client, claim.ClaimId, "Indemnity"));
    }

    [Fact]
    public async Task A_reversal_must_be_exactly_the_negated_balance()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 6_000m, ReserveTransactionType.Add);

        var response = await SubmitAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, -5_000m, ReserveTransactionType.Reverse);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("RESERVE-REVERSE-01", await BlockingCodeAsync(response));
    }

    [Fact]
    public async Task Approving_a_pending_decrease_that_would_now_take_the_balance_below_zero_is_rejected()
    {
        using var handler = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        using var supervisor = Fixture.CreateAuthenticatedClient("supervisor", SeedData.DemoSupervisorUserId);
        var claim = await CreateClaimWithPolicyAsync(handler);
        await SubmitAsync(handler, claim.ClaimId, ReserveComponentType.Indemnity, 30_000m, ReserveTransactionType.Add);
        var pendingIncrease = (await handler.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves"))!.History.Single();
        await supervisor.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{pendingIncrease.ReserveHistoryId}/approve", null); // balance 30,000
        var firstCut = (await (await SubmitAsync(handler, claim.ClaimId, ReserveComponentType.Indemnity, -20_000m, ReserveTransactionType.Adjust))
            .Content.ReadFromJsonAsync<ReserveTransactionDto>())!; // pending: 30k → 10k
        var secondCut = (await (await SubmitAsync(handler, claim.ClaimId, ReserveComponentType.Indemnity, -20_000m, ReserveTransactionType.Adjust))
            .Content.ReadFromJsonAsync<ReserveTransactionDto>())!; // pending: 30k → 10k as submitted
        await supervisor.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{firstCut.ReserveHistoryId}/approve", null); // balance 10,000

        var response = await supervisor.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{secondCut.ReserveHistoryId}/approve", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("RESERVE-BALANCE-NEGATIVE-01", await BlockingCodeAsync(response));
        Assert.Equal(10_000m, await BalanceAsync(handler, claim.ClaimId, "Indemnity"));
    }
}
