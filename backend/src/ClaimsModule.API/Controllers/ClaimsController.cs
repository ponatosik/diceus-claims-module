using ClaimsModule.API.Models;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.AddClaimParty;
using ClaimsModule.Application.Claims.ApproveReserve;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.OpenReserve;
using ClaimsModule.Application.Claims.RejectReserve;
using ClaimsModule.Application.Claims.RemoveClaimParty;
using ClaimsModule.Application.Claims.RetractReserve;
using ClaimsModule.Application.Claims.SetReserveOverride;
using ClaimsModule.Application.Claims.TransitionClaimStatus;
using ClaimsModule.Application.Claims.UploadClaimDocument;
using ClaimsModule.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/claims")]
public class ClaimsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ClaimDto>> CreateClaim([FromBody] CreateClaimCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetClaimDetail), new { id = result.ClaimId }, result);
    }

    [HttpPost("validate")]
    public async Task<ActionResult<List<ValidationIssueDto>>> ValidateClaim([FromBody] ValidateClaimQuery query, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClaimSummaryDto>>> ListClaims(
        [FromQuery] ClaimStatus? status,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] Guid? assignedHandlerId,
        [FromQuery] string? causeOfLossCode,
        [FromQuery] Guid? policyId,
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var query = new ListClaimsQuery(
            status, dateFrom, dateTo, assignedHandlerId, causeOfLossCode, policyId, search,
            normalizedPage, normalizedPageSize);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimDetailDto>> GetClaimDetail(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetClaimDetailQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> GetClaimAuditLog(
        Guid id, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var result = await mediator.Send(new GetClaimAuditLogQuery(id, normalizedPage, normalizedPageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Clamps caller-supplied paging to sane bounds — a 0 (unset) page/pageSize falls back to the
    /// documented default, but a negative value (whether from a bug or a malicious client) is
    /// clamped rather than reaching EF Core's Skip/Take as a negative OFFSET/FETCH, which SQL Server
    /// rejects and would otherwise surface as a 500.
    /// </summary>
    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (page <= 0 ? 1 : page, pageSize switch { 0 => 20, < 0 => 20, > 100 => 100, _ => pageSize });

    [HttpPost("{id:guid}/parties")]
    public async Task<ActionResult<ClaimPartyDto>> AddClaimParty(Guid id, [FromBody] AddClaimPartyCommand command, CancellationToken cancellationToken)
    {
        command.ClaimId = id;
        var result = await mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{id:guid}/parties/{partyId:guid}")]
    public async Task<IActionResult> RemoveClaimParty(Guid id, Guid partyId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveClaimPartyCommand(id, partyId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ClaimDto>> TransitionClaimStatus(Guid id, [FromBody] TransitionClaimStatusCommand command, CancellationToken cancellationToken)
    {
        command.ClaimId = id;
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reserves")]
    public async Task<ActionResult<ReserveTransactionDto>> OpenReserve(Guid id, [FromBody] OpenReserveCommand command, CancellationToken cancellationToken)
    {
        command.ClaimId = id;
        var result = await mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{id:guid}/reserves")]
    public async Task<ActionResult<ReserveListDto>> ListClaimReserves(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListClaimReservesQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reserves/{txnId:guid}/approve")]
    public async Task<ActionResult<ReserveTransactionDto>> ApproveReserve(Guid id, Guid txnId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApproveReserveCommand(id, txnId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reserves/{txnId:guid}/reject")]
    public async Task<ActionResult<ReserveTransactionDto>> RejectReserve(
        Guid id, Guid txnId, [FromBody] RejectReserveRequestBody body, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RejectReserveCommand(id, txnId, body.RejectionReason), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reserves/{txnId:guid}/retract")]
    public async Task<ActionResult<ReserveTransactionDto>> RetractReserve(Guid id, Guid txnId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RetractReserveCommand(id, txnId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reserves/override")]
    public async Task<IActionResult> SetReserveOverride(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetReserveOverrideCommand(id), cancellationToken);
        return NoContent();
    }

    // Raised above the 50MB business-rule limit (not set equal to it) — a file just over 50MB must
    // still reach UploadClaimDocumentCommandValidator and get a proper 422, not be rejected by
    // Kestrel/the multipart parser first as a generic, unvalidated 400 (see Resolved Ambiguity 7).
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(60_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60_000_000)]
    public async Task<ActionResult<ClaimDocumentDetailDto>> UploadDocument(
        Guid id, [FromForm] UploadClaimDocumentRequest request, CancellationToken cancellationToken)
    {
        var command = new UploadClaimDocumentCommand
        {
            ClaimId = id,
            DocumentType = request.DocumentType,
            Notes = request.Notes,
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
            FileSizeBytes = request.File.Length,
            Content = request.File.OpenReadStream(),
        };
        var result = await mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
