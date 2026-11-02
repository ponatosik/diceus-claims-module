using System.Net;
using System.Net.Http.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Infrastructure.BackgroundJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>Spec 006 story 11: §11.3's retry button for Failed GL postings, re-enqueued through the outbox under the same idempotency key (ADR-0004/0005).</summary>
[Collection(ApiCollection.Name)]
public class RetryGLPostingTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<ClaimDto> CreateClaimWithPolicyAsync(HttpClient client)
    {
        var policyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        var command = ClaimDetailHeaderTests.ValidCommand();
        command.PolicyId = policyId;
        return (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    private static async Task<ReserveTransactionDto> OpenAutoApprovedReserveAsync(HttpClient client, Guid claimId) =>
        (await (await client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new OpenReserveCommand
        {
            Component = ReserveComponentType.Indemnity,
            Amount = 5_000m,
            ChangeReason = "Initial estimate",
            TransactionType = ReserveTransactionType.Add,
        })).Content.ReadFromJsonAsync<ReserveTransactionDto>())!;

    private async Task<int> CountOutboxMessagesAsync(Guid reserveHistoryId, string idempotencyKey)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM OutboxMessages WHERE Payload LIKE @id AND Payload LIKE @key";
        command.Parameters.AddWithValue("@id", $"%{reserveHistoryId}%");
        command.Parameters.AddWithValue("@key", $"%{idempotencyKey}%");
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task FailPostingAsync(Guid reserveHistoryId)
    {
        using var scope = Fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GLPostingFailureHandler>()
            .RecordFinalFailureAsync(reserveHistoryId, "simulated GL outage");
    }

    [Fact]
    public async Task Retrying_a_failed_posting_resets_it_to_pending_and_re_enqueues_under_the_same_idempotency_key()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenAutoApprovedReserveAsync(client, claim.ClaimId);
        await FailPostingAsync(opened.ReserveHistoryId);

        var response = await client.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{opened.ReserveHistoryId}/retry-posting", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Pending", (await response.Content.ReadFromJsonAsync<ReserveTransactionDto>())!.PostingStatus);

        // Original auto-approval message + the retry's — both carrying the original key (ADR-0005).
        var originalKey = $"Reserve:{opened.ReserveComponentId}:Change:1";
        Assert.Equal(2, await CountOutboxMessagesAsync(opened.ReserveHistoryId, originalKey));

        var audit = (await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit"))!;
        Assert.Single(audit.Items, e => e.EventType == "GL_POSTING_RETRY_REQUESTED");

        using var scope = Fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>().ExecuteAsync(opened.ReserveHistoryId, "retry-job");
        var reserves = (await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves"))!;
        Assert.Equal("Posted", reserves.History.Single().PostingStatus);
    }

    [Fact]
    public async Task Retrying_a_posting_that_has_not_failed_is_rejected_with_422()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenAutoApprovedReserveAsync(client, claim.ClaimId); // PostingStatus = Pending

        var response = await client.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{opened.ReserveHistoryId}/retry-posting", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ErrorResponse>())!;
        Assert.Equal("GL-RETRY-NOT-FAILED-01", Assert.Single(body.BlockingConditions!).Code);
    }

    [Fact]
    public async Task Retrying_an_unknown_transaction_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);

        var response = await client.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{Guid.NewGuid()}/retry-posting", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
