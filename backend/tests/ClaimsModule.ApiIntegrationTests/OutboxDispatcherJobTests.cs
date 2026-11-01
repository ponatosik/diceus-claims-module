using System.Net.Http.Json;
using ClaimsModule.Application;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;
using ClaimsModule.Infrastructure.BackgroundJobs;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class OutboxDispatcherJobTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private async Task<int> CountAuditEntriesAsync(HttpClient client, Guid claimId, string eventType)
    {
        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claimId}/audit");
        return audit!.Items.Count(a => a.EventType == eventType);
    }

    [Fact]
    public async Task Dispatching_an_unprocessed_outbox_row_enqueues_the_GL_job_and_marks_it_processed()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m); // <= $10,000 -> AutoApproved, writes an outbox row

        using var scope = Fixture.Services.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var jobStorage = scope.ServiceProvider.GetRequiredService<JobStorage>();
        var statsBefore = jobStorage.GetMonitoringApi().GetStatistics();

        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcherJob>();
        await dispatcher.ExecuteAsync();

        var remaining = await outboxRepository.ListUnprocessedAsync();
        Assert.Empty(remaining);

        var statsAfter = jobStorage.GetMonitoringApi().GetStatistics();
        Assert.True(statsAfter.Enqueued > statsBefore.Enqueued);
    }

    [Fact]
    public async Task Dispatching_the_same_row_twice_before_either_is_marked_processed_is_still_harmless()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimWithPolicyAsync(client);
        var opened = await OpenReserveAsync(client, claim.ClaimId, ReserveComponentType.Indemnity, 5_000m);

        // Both scopes read the same still-unprocessed row before either dispatches/commits —
        // simulating a dispatcher crash-and-retry window (Review Focus item 5).
        using var firstScope = Fixture.Services.CreateScope();
        using var secondScope = Fixture.Services.CreateScope();
        var firstMessage = (await firstScope.ServiceProvider.GetRequiredService<IOutboxRepository>().ListUnprocessedAsync()).Single();
        var secondMessage = (await secondScope.ServiceProvider.GetRequiredService<IOutboxRepository>().ListUnprocessedAsync()).Single();

        await firstScope.ServiceProvider.GetRequiredService<OutboxDispatcherJob>().DispatchAsync(firstMessage);
        await firstScope.ServiceProvider.GetRequiredService<IOutboxRepository>().CommitChangesAsync();
        await secondScope.ServiceProvider.GetRequiredService<OutboxDispatcherJob>().DispatchAsync(secondMessage);
        await secondScope.ServiceProvider.GetRequiredService<IOutboxRepository>().CommitChangesAsync();

        // Both dispatches enqueued a real PostGLReserveChangeJob execution; run both directly
        // (bypassing Hangfire's own scheduler per this slice's testing decision) to prove the
        // duplicate enqueue is harmless end-to-end, not just that the dispatcher didn't throw.
        using var jobScope1 = Fixture.Services.CreateScope();
        using var jobScope2 = Fixture.Services.CreateScope();
        await jobScope1.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>()
            .ExecuteAsync(opened.ReserveHistoryId, opened.ReserveHistoryId.ToString(), "job-1");
        await jobScope2.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>()
            .ExecuteAsync(opened.ReserveHistoryId, opened.ReserveHistoryId.ToString(), "job-2");

        Assert.Equal(1, await CountAuditEntriesAsync(client, claim.ClaimId, "GL_POSTING_SIMULATED"));
    }
}
