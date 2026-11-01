using System.Net.Http.Json;
using ClaimsModule.Application;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;
using ClaimsModule.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>
/// Dedicated test class for GL posting job idempotency — the trickiest correctness area in the
/// backend, per ADR-0009's reserved space for exactly this.
/// </summary>
[Collection(ApiCollection.Name)]
public class GLPostingJobTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static async Task<int> CountAuditEntriesAsync(HttpClient client, Guid claimId, string eventType)
    {
        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claimId}/audit");
        return audit!.Items.Count(a => a.EventType == eventType);
    }

    [Fact]
    public async Task Posting_a_pending_reserve_sets_Posted_and_writes_the_GL_POSTING_SIMULATED_audit_entry()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m); // <= $10,000 -> AutoApproved

        using var scope = Fixture.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>();
        await job.ExecuteAsync(opened.ReserveHistoryId, "job-1");

        var reserves = await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves");
        Assert.Equal("Posted", reserves!.History.Single().PostingStatus);

        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit");
        var entry = Assert.Single(audit!.Items, a => a.EventType == "GL_POSTING_SIMULATED");
        Assert.Equal("DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = $5,000.00.", entry.Description);
    }

    [Fact]
    public async Task Running_the_job_twice_with_the_same_idempotency_key_produces_exactly_one_audit_entry()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m);
        using var scope = Fixture.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>();

        await job.ExecuteAsync(opened.ReserveHistoryId, "job-1");
        await job.ExecuteAsync(opened.ReserveHistoryId, "job-2");

        Assert.Equal(1, await CountAuditEntriesAsync(client, claim.ClaimId, "GL_POSTING_SIMULATED"));
    }

    [Fact]
    public async Task A_losing_concurrent_execution_is_swallowed_not_retried()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m);

        using var firstScope = Fixture.Services.CreateScope();
        using var secondScope = Fixture.Services.CreateScope();
        var firstJob = firstScope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>();
        var secondJob = secondScope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>();

        // Both scopes' DbContexts load the row (each with the same original RowVer) before either
        // writes anything, simulating two near-simultaneous executions racing on the same row.
        _ = await firstScope.ServiceProvider.GetRequiredService<IClaimReserveComponentRepository>()
            .GetHistoryByIdAsync(opened.ReserveHistoryId);
        _ = await secondScope.ServiceProvider.GetRequiredService<IClaimReserveComponentRepository>()
            .GetHistoryByIdAsync(opened.ReserveHistoryId);

        await firstJob.ExecuteAsync(opened.ReserveHistoryId, "job-1");
        await secondJob.ExecuteAsync(opened.ReserveHistoryId, "job-2"); // must not throw

        Assert.Equal(1, await CountAuditEntriesAsync(client, claim.ClaimId, "GL_POSTING_SIMULATED"));
    }

    [Fact]
    public async Task GLPostingFailureHandler_RecordFinalFailureAsync_sets_Failed_and_writes_GL_POSTING_FAILED()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m);

        using var scope = Fixture.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GLPostingFailureHandler>();
        await handler.RecordFinalFailureAsync(opened.ReserveHistoryId, "simulated transient failure");

        var reserves = await client.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves");
        Assert.Equal("Failed", reserves!.History.Single().PostingStatus);
        Assert.Equal(1, await CountAuditEntriesAsync(client, claim.ClaimId, "GL_POSTING_FAILED"));
    }
}
