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
public class ApproveReserveTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static Task<HttpResponseMessage> ApproveReserveAsync(HttpClient client, Guid claimId, Guid reserveHistoryId) =>
        client.PostAsync($"/api/claims/{claimId}/reserves/{reserveHistoryId}/approve", null);

    private async Task<bool> OutboxMessageExistsForAsync(Guid reserveHistoryId)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM OutboxMessages WHERE Payload LIKE @pattern";
        command.Parameters.AddWithValue("@pattern", $"%{reserveHistoryId}%");
        return (int)(await command.ExecuteScalarAsync())! > 0;
    }

    private async Task SetComponentBalanceViaSqlAsync(Guid claimId, decimal amount)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ClaimReserveComponents SET CurrentAmount = @amount WHERE ClaimId = @claimId";
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@claimId", claimId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetReserveOverrideAsync(Guid claimId)
    {
        using var managerClient = Fixture.CreateAuthenticatedClient("manager");
        await managerClient.PostAsync($"/api/claims/{claimId}/reserves/override", null);
    }

    [Fact]
    public async Task Full_manual_approve_path_updates_balance_and_writes_an_outbox_message()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);

        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var response = await ApproveReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReserveTransactionDto>();
        Assert.Equal("Approved", body!.ApprovalStatus);

        var detail = await handlerClient.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}");
        Assert.Equal(50_000m, detail!.Reserves.Single().CurrentAmount);
        Assert.True(await OutboxMessageExistsForAsync(opened.ReserveHistoryId));
    }

    [Fact]
    public async Task Self_approval_is_rejected_with_BR_R_03()
    {
        using var client = Fixture.CreateAuthenticatedClient("supervisor");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);

        var response = await ApproveReserveAsync(client, claim.ClaimId, opened.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("BR-R-03", body!.BlockingConditions![0].Code);
    }

    [Fact]
    public async Task Role_tier_insufficient_requires_manager_for_manager_tier_amounts()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 150_000m);

        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var bySupervisor = await ApproveReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bySupervisor.StatusCode);
        var body = await bySupervisor.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("BR-R-02", body!.BlockingConditions![0].Code);

        using var managerClient = Fixture.CreateAuthenticatedClient("manager");
        var byManager = await ApproveReserveAsync(managerClient, claim.ClaimId, opened.ReserveHistoryId);
        Assert.Equal(HttpStatusCode.OK, byManager.StatusCode);
    }

    [Fact]
    public async Task Exactly_100000_is_approvable_by_supervisor_but_100000_01_requires_manager()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claimAtBoundary = await CreateClaimWithPolicyAsync(handlerClient);
        var atBoundary = await OpenReserveAsync(handlerClient, claimAtBoundary.ClaimId, ReserveComponentType.Indemnity, 100_000m);
        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var atBoundaryResponse = await ApproveReserveAsync(supervisorClient, claimAtBoundary.ClaimId, atBoundary.ReserveHistoryId);
        Assert.Equal(HttpStatusCode.OK, atBoundaryResponse.StatusCode);

        var claimOverBoundary = await CreateClaimWithPolicyAsync(handlerClient);
        var overBoundary = await OpenReserveAsync(handlerClient, claimOverBoundary.ClaimId, ReserveComponentType.Indemnity, 100_000.01m);
        var overBoundaryBySupervisor = await ApproveReserveAsync(supervisorClient, claimOverBoundary.ClaimId, overBoundary.ReserveHistoryId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overBoundaryBySupervisor.StatusCode);

        using var managerClient = Fixture.CreateAuthenticatedClient("manager");
        var overBoundaryByManager = await ApproveReserveAsync(managerClient, claimOverBoundary.ClaimId, overBoundary.ReserveHistoryId);
        Assert.Equal(HttpStatusCode.OK, overBoundaryByManager.StatusCode);
    }

    [Fact]
    public async Task Approving_an_already_approved_transaction_is_rejected_as_not_pending()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m);
        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        await ApproveReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId);

        var secondAttempt = await ApproveReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondAttempt.StatusCode);
        var body = await secondAttempt.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("RESERVE-NOT-PENDING-01", body!.BlockingConditions![0].Code);
    }

    [Fact]
    public async Task BR_R_05_approval_crossing_10M_is_blocked_until_the_override_is_set()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claim.ClaimId, ReserveComponentType.Indemnity, 20_000m);
        await SetComponentBalanceViaSqlAsync(claim.ClaimId, 9_990_000m);

        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var blocked = await ApproveReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        var body = await blocked.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(body!.BlockingConditions!, c => c.Code == "BR-R-05");

        await SetReserveOverrideAsync(claim.ClaimId);

        var allowed = await ApproveReserveAsync(supervisorClient, claim.ClaimId, opened.ReserveHistoryId);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Approving_a_transaction_belonging_to_a_different_claim_returns_404()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claimA = await CreateClaimWithPolicyAsync(handlerClient);
        var claimB = await CreateClaimWithPolicyAsync(handlerClient);
        var opened = await OpenReserveAsync(handlerClient, claimA.ClaimId, ReserveComponentType.Indemnity, 50_000m);

        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var response = await ApproveReserveAsync(supervisorClient, claimB.ClaimId, opened.ReserveHistoryId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
