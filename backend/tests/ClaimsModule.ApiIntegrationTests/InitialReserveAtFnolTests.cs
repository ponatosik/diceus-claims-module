using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Enums;
using Microsoft.Data.SqlClient;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>
/// Spec 006 story 7: §11.2 Step 3's optional initial reserve, persisted in the same transaction as the
/// FNOL claim with exactly the authority, outbox and audit behaviour of POST /reserves.
/// </summary>
[Collection(ApiCollection.Name)]
public class InitialReserveAtFnolTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<CreateClaimCommand> CommandWithPolicyAsync(HttpClient client, CreateInitialReserveDto? initialReserve)
    {
        var command = ClaimDetailHeaderTests.ValidCommand();
        command.PolicyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        command.InitialReserve = initialReserve;
        return command;
    }

    private async Task<int> CountOutboxMessagesForAsync(Guid reserveHistoryId)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM OutboxMessages WHERE Payload LIKE @pattern";
        command.Parameters.AddWithValue("@pattern", $"%{reserveHistoryId}%");
        return (int)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task An_auto_approved_initial_reserve_moves_the_balance_enqueues_GL_posting_and_is_audited()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var command = await CommandWithPolicyAsync(client, new CreateInitialReserveDto(ReserveComponentType.Indemnity, 5_000m, "Initial estimate"));

        var response = await client.PostAsJsonAsync("/api/claims", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var claim = (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
        var reserves = (await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves"))!;
        var transaction = Assert.Single(reserves.History);
        Assert.Equal("AutoApproved", transaction.ApprovalStatus);
        Assert.Equal("Add", transaction.TransactionType);
        Assert.Equal(5_000m, Assert.Single(reserves.Components).CurrentAmount);
        Assert.Equal(1, await CountOutboxMessagesForAsync(transaction.ReserveHistoryId));

        var events = (await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit"))!.Items.Select(e => e.EventType).ToList();
        Assert.Contains("CLAIM_CREATED", events);
        Assert.Contains("RESERVE_CREATED", events);
        Assert.Contains("RESERVE_AUTO_APPROVED", events);
    }

    [Fact]
    public async Task A_supervisor_tier_initial_reserve_awaits_approval_without_moving_the_balance_or_enqueuing_GL_posting()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var command = await CommandWithPolicyAsync(client, new CreateInitialReserveDto(ReserveComponentType.Expense, 25_000m, "Legal costs expected"));

        var claim = (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;

        var reserves = (await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves"))!;
        var transaction = Assert.Single(reserves.History);
        Assert.Equal("PendingApproval", transaction.ApprovalStatus);
        Assert.Equal(0m, reserves.Components.Single().CurrentAmount);
        Assert.Equal(25_000m, reserves.Components.Single().PendingAmount);
        Assert.Equal(0, await CountOutboxMessagesForAsync(transaction.ReserveHistoryId));
    }

    [Fact]
    public async Task An_initial_reserve_without_a_policy_is_rejected_and_nothing_is_persisted()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var command = ClaimDetailHeaderTests.ValidCommand(); // no PolicyId
        command.InitialReserve = new CreateInitialReserveDto(ReserveComponentType.Indemnity, 5_000m, "Initial estimate");

        var response = await client.PostAsJsonAsync("/api/claims", command);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ErrorResponse>())!;
        Assert.Contains("InitialReserve", body.Errors!.Keys);
        Assert.Equal(0, (await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims"))!.TotalCount);
    }

    [Fact]
    public async Task The_validate_endpoint_reports_initial_reserve_issues_under_InitialReserve_property_names()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var command = await CommandWithPolicyAsync(client, new CreateInitialReserveDto(ReserveComponentType.Indemnity, 0m, ""));

        var issues = (await (await client.PostAsJsonAsync("/api/claims/validate", command)).Content.ReadFromJsonAsync<List<ValidationIssueDto>>())!;

        Assert.Contains(issues, i => i.PropertyName == "InitialReserve.Amount" && i.Severity == "Error");
        Assert.Contains(issues, i => i.PropertyName == "InitialReserve.ChangeReason" && i.Severity == "Error");
    }
}
