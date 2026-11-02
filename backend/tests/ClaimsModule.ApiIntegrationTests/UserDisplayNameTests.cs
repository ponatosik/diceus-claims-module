using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Claims.RejectReserve;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>Spec 006 story 12: "Submitted By" / "Approved By" / uploader show Demo User display names, not bare ids (§11.3).</summary>
[Collection(ApiCollection.Name)]
public class UserDisplayNameTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static async Task<ClaimDto> CreateClaimWithPolicyAsync(HttpClient client)
    {
        var command = ClaimDetailHeaderTests.ValidCommand();
        command.PolicyId = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL-2024-001001"))!.Single().PolicyId;
        return (await (await client.PostAsJsonAsync("/api/claims", command)).Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    private static async Task<ReserveTransactionDto> OpenPendingReserveAsync(HttpClient client, Guid claimId) =>
        (await (await client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", new OpenReserveCommand
        {
            Component = ReserveComponentType.Indemnity,
            Amount = 50_000m,
            ChangeReason = "Liability confirmed",
            TransactionType = ReserveTransactionType.Add,
        })).Content.ReadFromJsonAsync<ReserveTransactionDto>())!;

    [Fact]
    public async Task Reserve_transactions_name_their_submitter_and_approver()
    {
        using var handler = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        using var supervisor = Fixture.CreateAuthenticatedClient("supervisor", SeedData.DemoSupervisorUserId);
        var claim = await CreateClaimWithPolicyAsync(handler);
        var pending = await OpenPendingReserveAsync(handler, claim.ClaimId);

        var approved = (await (await supervisor.PostAsync($"/api/claims/{claim.ClaimId}/reserves/{pending.ReserveHistoryId}/approve", null))
            .Content.ReadFromJsonAsync<ReserveTransactionDto>())!;
        var listed = (await handler.GetFromJsonAsync<ReserveListDto>($"/api/claims/{claim.ClaimId}/reserves"))!.History.Single();

        Assert.Equal("Alex Morgan", pending.SubmittedByDisplayName);
        Assert.Equal("Priya Shah", approved.ApprovedByDisplayName);
        Assert.Equal("Alex Morgan", listed.SubmittedByDisplayName);
        Assert.Equal("Priya Shah", listed.ApprovedByDisplayName);
        Assert.Null(listed.RejectedByDisplayName);
    }

    [Fact]
    public async Task A_rejected_transaction_names_its_rejecter()
    {
        using var handler = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        using var manager = Fixture.CreateAuthenticatedClient("manager", SeedData.DemoManagerUserId);
        var claim = await CreateClaimWithPolicyAsync(handler);
        var pending = await OpenPendingReserveAsync(handler, claim.ClaimId);

        var rejected = (await (await manager.PostAsJsonAsync(
                $"/api/claims/{claim.ClaimId}/reserves/{pending.ReserveHistoryId}/reject", new RejectReserveRequestBody("Not supported by evidence")))
            .Content.ReadFromJsonAsync<ReserveTransactionDto>())!;

        Assert.Equal("Daniel Okafor", rejected.RejectedByDisplayName);
    }

    [Fact]
    public async Task Documents_name_their_uploader()
    {
        using var handler = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        var claim = await CreateClaimWithPolicyAsync(handler);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "File", "report.pdf");
        form.Add(new StringContent("PoliceReport"), "DocumentType");

        var uploaded = (await (await handler.PostAsync($"/api/claims/{claim.ClaimId}/documents", form)).Content.ReadFromJsonAsync<ClaimDocumentDetailDto>())!;
        var listed = (await handler.GetFromJsonAsync<List<ClaimDocumentDetailDto>>($"/api/claims/{claim.ClaimId}/documents"))!.Single();

        Assert.Equal("Alex Morgan", uploaded.UploadedByDisplayName);
        Assert.Equal("Alex Morgan", listed.UploadedByDisplayName);
    }

    [Fact]
    public async Task Audit_entries_carry_their_user_and_related_entity()
    {
        using var handler = Fixture.CreateAuthenticatedClient("handler", SeedData.DemoHandlerUserId);
        var claim = await CreateClaimWithPolicyAsync(handler);
        var pending = await OpenPendingReserveAsync(handler, claim.ClaimId);

        var audit = (await handler.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claim.ClaimId}/audit"))!;
        var reserveEntry = audit.Items.Single(e => e.EventType == "RESERVE_CREATED");
        var claimEntry = audit.Items.Single(e => e.EventType == "CLAIM_CREATED");

        Assert.NotEqual(Guid.Empty, reserveEntry.AuditLogId);
        Assert.Equal(SeedData.DemoHandlerUserId, reserveEntry.UserId);
        Assert.Equal("Alex Morgan", reserveEntry.UserDisplayName);
        Assert.Equal("ReserveHistory", reserveEntry.RelatedEntityType);
        Assert.Equal(pending.ReserveHistoryId, reserveEntry.RelatedEntityId);
        Assert.Null(claimEntry.RelatedEntityType); // claim-level entry: nothing to link to

        var recent = (await handler.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.ClaimId}"))!.RecentAuditEntries;
        Assert.Equal("Alex Morgan", recent.Single(e => e.EventType == "RESERVE_CREATED").UserDisplayName);
    }
}
