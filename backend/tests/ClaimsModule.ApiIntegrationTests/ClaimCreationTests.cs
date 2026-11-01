using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Policies;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class ClaimCreationTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static CreateClaimCommand ValidCommand(
        DateTimeOffset? lossDate = null,
        string? lossDescription = null,
        string? causeOfLossCode = null,
        Guid? policyId = null,
        List<CreatePartyDto>? parties = null) => new()
    {
        PolicyId = policyId,
        LossDate = lossDate ?? DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = lossDescription ?? "A pipe burst and flooded the basement overnight.",
        CauseOfLossCode = causeOfLossCode ?? "COL-FIRE",
        Parties = parties ?? [new CreatePartyDto(PartyRole.Claimant, PartyType.Person, "Jane", "Doe", null, null, null)],
        RiskObjects = [],
    };

    [Fact]
    public async Task Happy_path_creates_a_draft_claim_with_a_generated_claim_number()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Matches(new Regex(@"^CLM-\d{4}-\d{7}$"), body!.ClaimNumber);
        Assert.Equal("Draft", body.Status);
    }

    [Fact]
    public async Task BR_C_01_future_loss_date_is_rejected_with_422()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand(lossDate: DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Loss date cannot be in the future.", body!.Errors!["LossDate"]);
    }

    [Fact]
    public async Task BR_C_02_loss_date_outside_an_expired_policys_effective_period_is_a_warning_not_a_block()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");
        var searchResults = await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2023-000099");
        var expiredPolicyId = searchResults!.Single().PolicyId;

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            ValidCommand(lossDate: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), policyId: expiredPolicyId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Equal("Draft", body!.Status);
    }

    [Fact]
    public async Task BR_C_03_no_claimant_party_is_rejected_with_422()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand(parties: []));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("At least one Claimant party is required to open a claim.", body!.Errors!["Parties"]);
    }

    [Fact]
    public async Task BR_C_05_nonexistent_cause_of_loss_code_is_rejected_with_422()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand(causeOfLossCode: "COL-NOPE"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Cause of loss code is not recognised or is inactive.", body!.Errors!["CauseOfLossCode"]);
    }

    [Fact]
    public async Task BR_C_05_active_cause_of_loss_code_is_accepted()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand(causeOfLossCode: "COL-FIRE"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BR_C_06_null_policy_is_accepted_and_echoed_back_as_null()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand(policyId: null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.Null(body!.PolicyId);
    }

    [Fact]
    public async Task BR_C_07_loss_description_of_exactly_20_characters_is_accepted()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand(lossDescription: new string('a', 20)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BR_C_07_loss_description_of_19_characters_is_rejected_with_422()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var response = await client.PostAsJsonAsync("/api/claims", ValidCommand(lossDescription: new string('a', 19)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Loss description is required and must be at least 20 characters.", body!.Errors!["LossDescription"]);
    }

    [Fact]
    public async Task Concurrent_submissions_never_collide_on_claim_number()
    {
        using var client = Fixture.CreateAuthenticatedClient("handler");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => client.PostAsJsonAsync("/api/claims", ValidCommand())));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var claimNumbers = await Task.WhenAll(responses.Select(async r =>
            (await r.Content.ReadFromJsonAsync<ClaimDto>())!.ClaimNumber));
        Assert.Equal(10, claimNumbers.Distinct().Count());
    }
}
