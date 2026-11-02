using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.TransitionClaimStatus;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>Spec 006 stories 4–6: the dashboard's multi-status filter, handler text search and Cause of Loss column (§11.1).</summary>
[Collection(ApiCollection.Name)]
public class ClaimListFilterTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static Task TransitionAsync(HttpClient client, Guid claimId, ClaimStatus target, string? reason = null) =>
        client.PutAsJsonAsync($"/api/claims/{claimId}/status", new TransitionClaimStatusCommand { TargetStatus = target, Reason = reason });

    [Fact]
    public async Task Status_filter_accepts_several_statuses()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        await CreateClaimAsync(client); // stays Draft
        var open = await CreateClaimAsync(client);
        await TransitionAsync(client, open.ClaimId, ClaimStatus.Open);
        var withdrawn = await CreateClaimAsync(client);
        await TransitionAsync(client, withdrawn.ClaimId, ClaimStatus.Open);
        await TransitionAsync(client, withdrawn.ClaimId, ClaimStatus.Withdrawn, "Claimant withdrew.");

        var result = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims?status=Draft&status=Withdrawn");

        Assert.Equal(2, result!.TotalCount);
        Assert.Equal(["Draft", "Withdrawn"], result.Items.Select(i => i.Status).Order());
    }

    [Fact]
    public async Task Assigned_handler_filter_matches_part_of_the_handlers_display_name_case_insensitively()
    {
        using var handlerClient = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);       // Alex Morgan
        using var supervisorClient = Fixture.CreateAuthenticatedClient("supervisor", SeedData.DemoSupervisorUserId); // Priya Shah
        var handlersClaim = await CreateClaimAsync(handlerClient);
        await CreateClaimAsync(supervisorClient);

        var result = await handlerClient.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims?assignedHandler=morg");

        Assert.Equal(handlersClaim.ClaimId, Assert.Single(result!.Items).ClaimId);
    }

    [Fact]
    public async Task Assigned_handler_filter_matching_nobody_returns_no_claims()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        await CreateClaimAsync(client);

        var result = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims?assignedHandler=nobody-by-this-name");

        Assert.Equal(0, result!.TotalCount);
    }

    [Fact]
    public async Task Summary_rows_carry_the_cause_of_loss_code_and_name()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        await CreateClaimAsync(client);

        var row = (await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims"))!.Items.Single();

        Assert.Equal("COL-FIRE", row.CauseOfLossCode);
        Assert.Equal("Fire", row.CauseOfLossName);
    }
}
