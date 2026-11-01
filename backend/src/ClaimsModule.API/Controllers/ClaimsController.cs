using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.CreateClaim;
using ClaimsModule.Application.Claims.Dtos;
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
        var query = new ListClaimsQuery(
            status, dateFrom, dateTo, assignedHandlerId, causeOfLossCode, policyId, search,
            page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize);
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
        var result = await mediator.Send(
            new GetClaimAuditLogQuery(id, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), cancellationToken);
        return Ok(result);
    }
}
