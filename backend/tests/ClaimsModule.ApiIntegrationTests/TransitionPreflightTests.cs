using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Claims.TransitionClaimStatus;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Enums;
using Microsoft.Data.SqlClient;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>
/// Spec 006 story 14: GET /api/claims/{id}/status/preflight — the Pre-flight Check (GLOSSARY.md) behind
/// §11.3's Closed-transition checklist. Same rules as PUT /status, evaluated without changing anything.
/// </summary>
[Collection(ApiCollection.Name)]
public class TransitionPreflightTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<ClaimDto> CreateOpenClaimAsync(HttpClient client)
    {
        var command = ClaimDetailHeaderTests.ValidCommand();
        command.PolicyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        var claim = (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;
        await client.PutAsJsonAsync($"/api/claims/{claim.ClaimId}/status", new TransitionClaimStatusCommand { TargetStatus = ClaimStatus.Open });
        return claim;
    }

    private static Task OpenReserveAsync(HttpClient client, Guid claimId, decimal amount) =>
        client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new OpenReserveCommand
        {
            Component = ReserveComponentType.Indemnity,
            Amount = amount,
            ChangeReason = "Estimate",
            TransactionType = ReserveTransactionType.Add,
        });

    private static async Task<TransitionPreflightDto> PreflightAsync(HttpClient client, Guid claimId, ClaimStatus target) =>
        (await client.GetFromJsonAsync<TransitionPreflightDto>($"/api/claims/{claimId}/status/preflight?target={target}"))!;

    [Fact]
    public async Task Closing_a_claim_with_a_pending_reserve_and_an_open_balance_reports_CC_01_as_blocking_and_CC_04_as_a_warning()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateOpenClaimAsync(client);
        await OpenReserveAsync(client, claim.ClaimId, 5_000m);   // auto-approved → open balance (CC-04)
        await OpenReserveAsync(client, claim.ClaimId, 50_000m);  // pending approval (CC-01)

        var preflight = await PreflightAsync(client, claim.ClaimId, ClaimStatus.Closed);

        Assert.True(preflight.IsTransitionValid);
        Assert.Equal("CC-01", Assert.Single(preflight.BlockingConditions).Code);
        Assert.Equal("CC-04", Assert.Single(preflight.Warnings).Code);
        Assert.True(preflight.RequiresJustificationNote);
        Assert.False(preflight.RequiresReason);

        // Nothing changed: the claim is still Open.
        Assert.Equal("Open", (await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}"))!.Status);
    }

    [Fact]
    public async Task Closing_a_claim_with_no_active_claimant_reports_CC_03_as_blocking()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateOpenClaimAsync(client);
        // The API refuses to remove the last Claimant, so the CC-03 state is seeded directly.
        await using (var connection = new SqlConnection(Fixture.ClaimsConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE ClaimParties SET IsActive = 0 WHERE ClaimId = @claimId";
            command.Parameters.AddWithValue("@claimId", claim.ClaimId);
            await command.ExecuteNonQueryAsync();
        }

        var preflight = await PreflightAsync(client, claim.ClaimId, ClaimStatus.Closed);

        Assert.Equal("CC-03", Assert.Single(preflight.BlockingConditions).Code);
    }

    [Fact]
    public async Task Closing_a_clean_claim_has_nothing_blocking()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateOpenClaimAsync(client);

        var preflight = await PreflightAsync(client, claim.ClaimId, ClaimStatus.Closed);

        Assert.True(preflight.IsTransitionValid);
        Assert.Empty(preflight.BlockingConditions);
        Assert.Empty(preflight.Warnings);
        Assert.False(preflight.RequiresJustificationNote);
    }

    [Fact]
    public async Task A_transition_outside_the_state_machine_is_reported_as_invalid()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var draft = (await (await client.PostAsJsonAsync("/api/claims", ClaimDetailHeaderTests.ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>())!;

        var preflight = await PreflightAsync(client, draft.ClaimId, ClaimStatus.Closed);

        Assert.False(preflight.IsTransitionValid);
        Assert.Equal("BR-ST-01", Assert.Single(preflight.BlockingConditions).Code);
    }

    [Fact]
    public async Task Withdrawal_needs_a_reason_but_a_missing_reason_is_not_reported_as_blocking()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var claim = await CreateOpenClaimAsync(client);

        var preflight = await PreflightAsync(client, claim.ClaimId, ClaimStatus.Withdrawn);

        Assert.True(preflight.RequiresReason);
        Assert.Empty(preflight.BlockingConditions);
    }

    [Fact]
    public async Task Preflight_for_an_unknown_claim_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync($"/api/claims/{Guid.NewGuid()}/status/preflight?target=Closed");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
