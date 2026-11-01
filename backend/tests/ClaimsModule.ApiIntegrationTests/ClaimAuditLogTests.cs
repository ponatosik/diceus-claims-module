using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain;
using Microsoft.Data.SqlClient;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class ClaimAuditLogTests(ApiFixture fixture) : ApiTestBase(fixture)
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
    public async Task Returns_the_creation_entry_newest_first()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var created = await (await client.PostAsJsonAsync("/api/claims", ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>();

        var result = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{created!.ClaimId}/audit");

        Assert.Contains(result!.Items, e => e.EventType == "CLAIM_CREATED");
        Assert.Equal(result.Items.OrderByDescending(e => e.CreatedAt).Select(e => e.CreatedAt), result.Items.Select(e => e.CreatedAt));
    }

    [Fact]
    public async Task A_warning_severity_issue_at_creation_is_recorded_as_a_validation_issue_added_entry()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        // PolicyId = null always triggers BR-C-06's Warning.
        var created = await (await client.PostAsJsonAsync("/api/claims", ValidCommand(policyId: null))).Content.ReadFromJsonAsync<ClaimDto>();

        var result = await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{created!.ClaimId}/audit");

        Assert.Contains(result!.Items, e => e.EventType == "VALIDATION_ISSUE_ADDED");
    }

    [Fact]
    public async Task Audit_for_an_unknown_claim_returns_404()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.GetAsync($"/api/claims/{Guid.NewGuid()}/audit");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_creation_entry_records_who_did_it()
    {
        var userId = Guid.NewGuid();
        using var client = Fixture.CreateAuthenticatedClient("handler", userId);
        var created = await (await client.PostAsJsonAsync("/api/claims", ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>();

        var createdByUserId = await GetCreatedByUserIdAsync(created!.ClaimId, "CLAIM_CREATED");

        Assert.Equal(userId, createdByUserId);
    }

    private async Task<Guid?> GetCreatedByUserIdAsync(Guid claimId, string eventType)
    {
        await using var connection = new SqlConnection(Fixture.ClaimsConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CreatedByUserId FROM ClaimAuditLog WHERE ClaimId = @claimId AND EventType = @eventType";
        command.Parameters.AddWithValue("@claimId", claimId);
        command.Parameters.AddWithValue("@eventType", eventType);
        var result = await command.ExecuteScalarAsync();
        return result is Guid guid ? guid : null;
    }
}
