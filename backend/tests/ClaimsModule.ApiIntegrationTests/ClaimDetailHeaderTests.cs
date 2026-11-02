using System.Net.Http.Json;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>Spec 006 story 8: the fields §11.3's header and Overview tab need on GET /api/claims/{id}.</summary>
[Collection(ApiCollection.Name)]
public class ClaimDetailHeaderTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    internal static CreateClaimCommand ValidCommand() => new()
    {
        LossDate = DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = "A pipe burst and flooded the basement overnight.",
        CauseOfLossCode = "COL-FIRE",
        Parties = [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
        RiskObjects = [],
    };

    [Fact]
    public async Task Detail_carries_assigned_handler_cause_of_loss_name_and_unassessed_severity()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        var created = (await (await client.PostAsJsonAsync("/api/claims", ValidCommand())).Content.ReadFromJsonAsync<ClaimDto>())!;

        var detail = (await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{created.ClaimId}"))!;

        Assert.Equal(SeedData.DemoHandlerUserId, detail.AssignedHandler!.UserId);
        Assert.Equal("Alex Morgan", detail.AssignedHandler.DisplayName);
        Assert.Equal("Fire", detail.CauseOfLossName);
        Assert.Null(detail.Severity); // nothing assigns severity; the UI shows "Not assessed" (spec 006 Further Notes)
        Assert.Null(detail.Notes);
    }
}
