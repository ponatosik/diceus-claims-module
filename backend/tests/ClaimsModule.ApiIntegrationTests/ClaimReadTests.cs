using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class ClaimReadTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static CreateClaimCommand ValidCommand(Guid? policyId = null) => new()
    {
        PolicyId = policyId,
        LossDate = DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = "A pipe burst and flooded the basement overnight.",
        CauseOfLossCode = "COL-FIRE",
        Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
        RiskObjects = [],
    };

    [Fact]
    public async Task Lists_claims_filtered_by_status_and_search()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        // ClientName is denormalised from the linked policy — search-by-ClientName needs a real
        // policy link, not just arbitrary party names.
        var meridianId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=Meridian"))!.Single().PolicyId;
        var harborviewId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=Harborview"))!.Single().PolicyId;
        await client.PostAsJsonAsync("/api/claims", ValidCommand(meridianId));
        await client.PostAsJsonAsync("/api/claims", ValidCommand(harborviewId));

        var byStatus = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims?status=Draft");
        Assert.Equal(2, byStatus!.TotalCount);

        var bySearch = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims?search=Meridian");
        Assert.Equal(1, bySearch!.TotalCount);
        Assert.Equal("Meridian Transport LLC", bySearch.Items.Single().ClientName);
    }

    [Fact]
    public async Task A_claim_with_no_reserves_shows_zero_total_reserves()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        await client.PostAsJsonAsync("/api/claims", ValidCommand());

        var result = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims");

        Assert.Equal(0m, result!.Items.Single().TotalReserves);
    }

    [Fact]
    public async Task Pagination_limits_items_but_reports_the_full_total_count()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        await client.PostAsJsonAsync("/api/claims", ValidCommand());
        await client.PostAsJsonAsync("/api/claims", ValidCommand());
        await client.PostAsJsonAsync("/api/claims", ValidCommand());

        var result = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>("/api/claims?page=1&pageSize=1");

        Assert.Equal(3, result!.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Detail_returns_the_full_claim_including_empty_reserves_documents_and_the_creation_audit_entry()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var created = await (await client.PostAsJsonAsync("/api/claims", ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>();

        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{created!.ClaimId}");

        Assert.Equal(created.ClaimId, detail!.ClaimId);
        Assert.Empty(detail.Reserves);
        Assert.Empty(detail.Documents);
        Assert.Contains(detail.RecentAuditEntries, e => e.EventType == "CLAIM_CREATED");
    }

    [Fact]
    public async Task Detail_for_an_unknown_id_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync($"/api/claims/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
