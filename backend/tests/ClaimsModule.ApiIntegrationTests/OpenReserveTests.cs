using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;
using Microsoft.Data.SqlClient;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class OpenReserveTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static Task<HttpResponseMessage> OpenReserveAsync(
        HttpClient client, Guid claimId, ReserveComponentType component, decimal amount, string changeReason = "Initial estimate") =>
        client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new OpenReserveCommand
        {
            Component = component,
            Amount = amount,
            ChangeReason = changeReason,
            TransactionType = ReserveTransactionType.Add,
        });

    private async Task SetReserveOverrideAsync(Guid claimId)
    {
        using var managerClient = Fixture.CreateAuthenticatedClient("manager");
        await managerClient.PostAsync($"/api/claims/{claimId}/reserves/override", null);
    }

    [Fact]
    public async Task Happy_path_auto_approves_and_updates_the_component_balance()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var response = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReserveTransactionDto>();
        Assert.Equal("AutoApproved", body!.ApprovalStatus);
        Assert.Equal(5_000m, body.NewBalance);

        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.Equal(5_000m, detail!.Reserves.Single().CurrentAmount);
    }

    [Fact]
    public async Task Exactly_10000_auto_approves()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var response = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 10_000m);

        var body = await response.Content.ReadFromJsonAsync<ReserveTransactionDto>();
        Assert.Equal("AutoApproved", body!.ApprovalStatus);
    }

    [Fact]
    public async Task Just_over_10000_requires_approval_and_does_not_move_the_balance()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var response = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 10_000.01m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReserveTransactionDto>();
        Assert.Equal("PendingApproval", body!.ApprovalStatus);

        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.Equal(0m, detail!.Reserves.Single().CurrentAmount);
    }

    [Fact]
    public async Task Exactly_100000_still_just_requires_approval()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var response = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 100_000m);

        var body = await response.Content.ReadFromJsonAsync<ReserveTransactionDto>();
        Assert.Equal("PendingApproval", body!.ApprovalStatus);
    }

    [Fact]
    public async Task BR_R_01_negative_amount_is_rejected_for_indemnity_but_allowed_for_subrogation_recoverable()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var indemnityResponse = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, -500m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, indemnityResponse.StatusCode);
        var errorBody = await indemnityResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Reserve amount must be greater than zero.", errorBody!.Errors!["Amount"]);

        var subrogationResponse = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.SubrogationRecoverable, -500m);
        Assert.Equal(HttpStatusCode.Created, subrogationResponse.StatusCode);
    }

    [Fact]
    public async Task An_out_of_range_component_value_is_rejected_as_invalid()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var response = await client.PostAsJsonAsync($"/api/claims/{claim.ClaimId}/reserves",
            new { component = 99, amount = 1_000m, changeReason = "test", transactionType = "Add" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid reserve component type.", body!.Errors!["Component"]);
    }

    [Fact]
    public async Task An_out_of_range_transaction_type_value_is_rejected_as_invalid()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var response = await client.PostAsJsonAsync($"/api/claims/{claim.ClaimId}/reserves",
            new { component = "Indemnity", amount = 1_000m, changeReason = "test", transactionType = 99 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid reserve transaction type.", body!.Errors!["TransactionType"]);
    }

    [Fact]
    public async Task A_second_submission_for_the_same_component_adjusts_it_rather_than_duplicating_it()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 3_000m);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 2_000m);

        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.Single(detail!.Reserves);
        Assert.Equal(5_000m, detail.Reserves[0].CurrentAmount);
    }

    [Fact]
    public async Task BR_C_06_reserve_creation_is_blocked_not_just_warned_when_no_policy_is_linked()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var command = new CreateClaimCommand
        {
            PolicyId = null,
            LossDate = DateTimeOffset.UtcNow.AddDays(-1),
            LossDescription = "A pipe burst and flooded the basement overnight.",
            CauseOfLossCode = "COL-FIRE",
            Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
            RiskObjects = [],
        };
        var claim = (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;

        var response = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("No policy linked. Policy must be associated before reserves can be set.", body!.Errors!["ClaimId"]);
    }

    [Fact]
    public async Task BR_R_05_two_auto_approving_reserves_that_would_exceed_10M_are_blocked_until_the_override_is_set()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var first = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 10_000m);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Push the claim's total near the $10M line by setting the component's balance directly via
        // SQL (cheaper than driving a real approval flow just to get a component near the limit),
        // then prove the next auto-approving submission is blocked by the aggregate check.
        await using (var connection = new SqlConnection(Fixture.ClaimsConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE ClaimReserveComponents SET CurrentAmount = 9999995 WHERE ClaimId = @claimId";
            command.Parameters.AddWithValue("@claimId", claim.ClaimId);
            await command.ExecuteNonQueryAsync();
        }

        var blocked = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Expense, 10_000m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        var blockedBody = await blocked.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(blockedBody!.BlockingConditions!, c => c.Code == "BR-R-05");

        await SetReserveOverrideAsync(claim.ClaimId);

        var allowed = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Expense, 10_000m);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }
}
