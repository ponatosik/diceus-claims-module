using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.TransitionClaimStatus;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Infrastructure.BackgroundJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class SlaMonitoringJobTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static CreateClaimCommand ValidCommand() => new()
    {
        LossDate = DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = "A pipe burst and flooded the basement overnight.",
        CauseOfLossCode = "COL-FIRE",
        Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
        RiskObjects = [],
    };

    private static async Task<ClaimDto> CreateClaimAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/claims", ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>())!;

    private static Task<HttpResponseMessage> TransitionAsync(HttpClient client, Guid claimId, ClaimStatus targetStatus, string? reason = null) =>
        client.PutAsJsonAsync($"/api/claims/{claimId}/status", new TransitionClaimStatusCommand { TargetStatus = targetStatus, Reason = reason });

    private async Task BackdateClaimAsync(Guid claimId, DateTimeOffset createdAt)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Claims SET CreatedAt = @createdAt, UpdatedAt = NULL WHERE ClaimId = @claimId";
        command.Parameters.AddWithValue("@createdAt", createdAt);
        command.Parameters.AddWithValue("@claimId", claimId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task BackdateLastBreachAsync(Guid claimId, DateTimeOffset createdAt)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ClaimAuditLog SET CreatedAt = @createdAt
            WHERE AuditLogId = (
                SELECT TOP 1 AuditLogId FROM ClaimAuditLog
                WHERE ClaimId = @claimId AND EventType = 'SLA_BREACH_DETECTED'
                ORDER BY CreatedAt DESC);
            """;
        command.Parameters.AddWithValue("@createdAt", createdAt);
        command.Parameters.AddWithValue("@claimId", claimId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountBreachEntriesAsync(HttpClient client, Guid claimId)
    {
        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claimId}/audit");
        return audit!.Items.Count(a => a.EventType == "SLA_BREACH_DETECTED");
    }

    [Fact]
    public async Task A_stale_Draft_claim_gets_a_SLA_BREACH_DETECTED_entry()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await BackdateClaimAsync(claim.ClaimId, DateTimeOffset.UtcNow.AddHours(-50));

        using var scope = Fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>().ExecuteAsync();

        var audit = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit");
        var entry = Assert.Single(audit!.Items, a => a.EventType == "SLA_BREACH_DETECTED");
        Assert.Equal("Claim has not been updated in 48 hours.", entry.Description);
    }

    [Fact]
    public async Task Running_it_again_within_24_hours_does_not_duplicate_the_breach()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await BackdateClaimAsync(claim.ClaimId, DateTimeOffset.UtcNow.AddHours(-50));

        using var scope = Fixture.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>();
        await job.ExecuteAsync();
        await job.ExecuteAsync();

        Assert.Equal(1, await CountBreachEntriesAsync(client, claim.ClaimId));
    }

    [Fact]
    public async Task A_breach_more_than_24_hours_old_allows_a_new_one()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await BackdateClaimAsync(claim.ClaimId, DateTimeOffset.UtcNow.AddHours(-50));

        using var scope = Fixture.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>();
        await job.ExecuteAsync();
        await BackdateLastBreachAsync(claim.ClaimId, DateTimeOffset.UtcNow.AddHours(-25));

        await job.ExecuteAsync();

        Assert.Equal(2, await CountBreachEntriesAsync(client, claim.ClaimId));
    }

    [Fact]
    public async Task A_breach_less_than_24_hours_old_suppresses_a_new_one()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await BackdateClaimAsync(claim.ClaimId, DateTimeOffset.UtcNow.AddHours(-50));

        using var scope = Fixture.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>();
        await job.ExecuteAsync();
        await BackdateLastBreachAsync(claim.ClaimId, DateTimeOffset.UtcNow.AddHours(-23));

        await job.ExecuteAsync();

        Assert.Equal(1, await CountBreachEntriesAsync(client, claim.ClaimId));
    }

    [Fact]
    public async Task A_Closed_claim_is_never_flagged_even_if_stale()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateClaimAsync(client);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Open);
        await TransitionAsync(client, claim.ClaimId, ClaimStatus.Closed);
        await BackdateClaimAsync(claim.ClaimId, DateTimeOffset.UtcNow.AddHours(-100));

        using var scope = Fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>().ExecuteAsync();

        Assert.Equal(0, await CountBreachEntriesAsync(client, claim.ClaimId));
    }
}
