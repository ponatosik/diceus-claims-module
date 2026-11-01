using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Claims.TransitionClaimStatus;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;
using Microsoft.Data.SqlClient;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class StatusTransitionTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static CreateClaimCommand ValidCommand() => new()
    {
        LossDate = DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = "A pipe burst and flooded the basement overnight.",
        CauseOfLossCode = "COL-FIRE",
        Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
        RiskObjects = [],
    };

    private async Task<ClaimDto> CreateClaimAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/claims", ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>())!;

    /// <summary>OpenReserveCommand requires a linked policy (BR-C-06's consequence) — ValidCommand()'s claims have none.</summary>
    private async Task<ClaimDto> CreateClaimWithPolicyAsync(HttpClient client)
    {
        var policyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        var command = ValidCommand();
        command.PolicyId = policyId;
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

    private static Task<HttpResponseMessage> TransitionAsync(
        HttpClient client, Guid claimId, ClaimStatus targetStatus, string? reason = null, string? justificationNote = null) =>
        client.PutAsJsonAsync($"/api/claims/{claimId}/status", new TransitionClaimStatusCommand
        {
            TargetStatus = targetStatus,
            Reason = reason,
            JustificationNote = justificationNote,
        });

    private async Task<Guid> InsertReserveComponentAsync(Guid claimId, decimal currentAmount, ReserveComponentStatus status = ReserveComponentStatus.Active)
    {
        var reserveComponentId = Guid.NewGuid();
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ClaimReserveComponents (ReserveComponentId, OrganisationId, ClaimId, Component, CurrentAmount, Status, IsDeleted, CreatedAt)
            VALUES (@id, '00000000-0000-0000-0000-000000000001', @claimId, 'Indemnity', @amount, @status, 0, SYSUTCDATETIME());
            """;
        command.Parameters.AddWithValue("@id", reserveComponentId);
        command.Parameters.AddWithValue("@claimId", claimId);
        command.Parameters.AddWithValue("@amount", currentAmount);
        command.Parameters.AddWithValue("@status", status.ToString());
        await command.ExecuteNonQueryAsync();
        return reserveComponentId;
    }

    private async Task InsertReserveHistoryAsync(Guid reserveComponentId, Guid claimId, ReserveApprovalStatus approvalStatus, decimal amount)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ReserveHistory
                (ReserveHistoryId, OrganisationId, ReserveComponentId, ClaimId, TransactionType, Amount, PreviousBalance, NewBalance,
                 ApprovalStatus, ChangeReason, PostingStatus, IdempotencyKey, ChangeSequence, CreatedAt)
            VALUES
                (NEWID(), '00000000-0000-0000-0000-000000000001', @reserveComponentId, @claimId, 'Add', @amount, 0, @amount,
                 @approvalStatus, 'test setup', 'Pending', @idempotencyKey, 1, SYSUTCDATETIME());
            """;
        command.Parameters.AddWithValue("@reserveComponentId", reserveComponentId);
        command.Parameters.AddWithValue("@claimId", claimId);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@approvalStatus", approvalStatus.ToString());
        command.Parameters.AddWithValue("@idempotencyKey", $"Reserve:{reserveComponentId}:Change:1");
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Draft_to_Open_happy_path_succeeds()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("Open", body!.Status);
    }

    [Fact]
    public async Task Invalid_transition_Draft_to_Closed_is_rejected_with_422_BR_ST_01_listing_valid_next_statuses()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);

        var response = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("BR-ST-01", body!.BlockingConditions![0].Code);
        Assert.Contains("Open", body.BlockingConditions[0].Message);
    }

    [Fact]
    public async Task Closing_with_a_pending_approval_reserve_is_blocked_by_CC_01()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        var componentId = await InsertReserveComponentAsync(claim.ClaimId, 1000m);
        await InsertReserveHistoryAsync(componentId, claim.ClaimId, ReserveApprovalStatus.PendingApproval, 1000m);

        var response = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(body!.BlockingConditions!, c => c.Code == "CC-01");
    }

    [Fact]
    public async Task Closing_a_claim_with_no_reserves_at_all_succeeds()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);

        var response = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("Closed", body!.Status);
    }

    [Fact]
    public async Task CC_04_open_reserves_block_closure_until_a_justification_note_is_given()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        await InsertReserveComponentAsync(claim.ClaimId, 500m);

        var withoutNote = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutNote.StatusCode);
        var body = await withoutNote.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(body!.BlockingConditions!, c => c.Code == "CC-04");

        var withNote = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed, justificationNote: "approved by ops");
        Assert.Equal(HttpStatusCode.OK, withNote.StatusCode);
    }

    [Fact]
    public async Task Open_to_PendingPayment_requires_an_approved_reserve()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);

        var withoutReserve = await TransitionAsync(client, claim.ClaimId, ClaimStatus.PendingPayment);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutReserve.StatusCode);
        var body = await withoutReserve.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(body!.BlockingConditions!, c => c.Code == "TRANS-PENDINGPAYMENT-01");

        var componentId = await InsertReserveComponentAsync(claim.ClaimId, 0m);
        await InsertReserveHistoryAsync(componentId, claim.ClaimId, ReserveApprovalStatus.AutoApproved, 5000m);

        var withReserve = await TransitionAsync(client, claim.ClaimId, ClaimStatus.PendingPayment);
        Assert.Equal(HttpStatusCode.OK, withReserve.StatusCode);
    }

    [Fact]
    public async Task Reopen_enforces_role_and_reason_then_cascades_to_Open_with_full_audit_trail()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(handlerClient);
        await TransitionAsync(handlerClient, claim.ClaimId, ClaimStatus.Open);
        await TransitionAsync(handlerClient, claim.ClaimId, ClaimStatus.Closed);

        var asHandler = await TransitionAsync(handlerClient, claim.ClaimId, ClaimStatus.Reopened, reason: "customer disputed settlement");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, asHandler.StatusCode);
        var handlerBody = await asHandler.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(handlerBody!.BlockingConditions!, c => c.Code == "BR-ST-04");

        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor");
        var emptyReason = await TransitionAsync(supervisorClient, claim.ClaimId, ClaimStatus.Reopened, reason: "   ");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, emptyReason.StatusCode);

        var withReason = await TransitionAsync(supervisorClient, claim.ClaimId, ClaimStatus.Reopened, reason: "customer disputed settlement");
        Assert.Equal(HttpStatusCode.OK, withReason.StatusCode);
        var body = await withReason.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("Open", body!.Status);

        var audit = await supervisorClient.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit");
        var eventTypes = audit!.Items.OrderBy(e => e.CreatedAt).Select(e => e.EventType).ToList();
        var reopenSequenceStart = eventTypes.IndexOf("CLAIM_REOPENED") - 1;
        Assert.True(reopenSequenceStart >= 0);
        Assert.Equal(
            ["STATUS_CHANGED", "CLAIM_REOPENED", "STATUS_CHANGED"],
            eventTypes.Skip(reopenSequenceStart).Take(3));
    }

    [Fact]
    public async Task Withdraw_requires_a_non_empty_reason()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);

        var withoutReason = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Withdrawn, reason: "");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutReason.StatusCode);

        var withReason = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Withdrawn, reason: "Claimant withdrew the claim.");
        Assert.Equal(HttpStatusCode.OK, withReason.StatusCode);
    }

    [Fact]
    public async Task Withdrawal_reason_is_recorded_in_the_audit_trail()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Withdrawn, reason: "Claimant withdrew the claim.");

        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit");

        Assert.Contains(audit!.Items, e => e.EventType == "STATUS_CHANGED" && e.Description.Contains("Claimant withdrew the claim."));
    }

    [Fact]
    public async Task CC_04_justification_note_is_recorded_in_the_audit_trail()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        await InsertReserveComponentAsync(claim.ClaimId, 500m);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed, justificationNote: "approved by ops");

        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit");

        Assert.Contains(audit!.Items, e => e.EventType == "CLAIM_CLOSED" && e.Description.Contains("approved by ops"));
    }

    [Fact]
    public async Task A_manager_can_reopen_a_closed_claim()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(handlerClient);
        await TransitionAsync(handlerClient, claim.ClaimId, ClaimStatus.Open);
        await TransitionAsync(handlerClient, claim.ClaimId, ClaimStatus.Closed);

        using var managerClient = Fixture.CreateAuthenticatedClient("manager");
        var response = await TransitionAsync(managerClient, claim.ClaimId, ClaimStatus.Reopened, reason: "manager-initiated reopen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("Open", body!.Status);
    }

    [Fact]
    public async Task Negative_pagination_values_are_clamped_rather_than_crashing()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        await CreateClaimAsync(client);

        var response = await client.GetAsync("/api/claims?page=-1&pageSize=-5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Slice C's own "Further Notes": re-verify these conditions against real reserve data written
    // through the actual OpenReserveCommand/approve endpoints, not just the raw-SQL rows above —
    // those stay as cheap, narrowly-scoped proofs of the condition logic itself.

    [Fact]
    public async Task CC_01_blocks_closure_when_a_real_reserve_is_left_PendingApproval()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 50_000m); // Supervisor tier -> PendingApproval, never approved

        var response = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(body!.BlockingConditions!, c => c.Code == "CC-01");
    }

    [Fact]
    public async Task Open_to_PendingPayment_succeeds_once_a_real_reserve_has_been_submitted_and_approved()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m); // <= $10,000 -> AutoApproved immediately

        var response = await TransitionAsync(client, claim.ClaimId, ClaimStatus.PendingPayment);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Closed_succeeds_once_the_real_reserve_is_genuinely_approved_and_nothing_is_pending()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m); // AutoApproved -> no PendingApproval rows remain

        // CC-01 is satisfied (nothing PendingApproval), but CC-04 still requires a justification note
        // since the approved reserve leaves a nonzero balance — this is the real-data equivalent of
        // StatusTransitionTests' existing CC_04_... test, proving the two conditions compose correctly.
        var response = await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed, justificationNote: "Reserve amount is final; no further activity expected.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("Closed", body!.Status);
    }
}
