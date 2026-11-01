using ClaimsModule.Application.Claims;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reference")]
public class ReferenceController(IMediator mediator) : ControllerBase
{
    [HttpGet("claim-statuses")]
    public async Task<ActionResult<List<ClaimStatusDto>>> GetClaimStatuses(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListClaimStatusesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("cause-of-loss-codes")]
    public async Task<ActionResult<List<CauseOfLossCodeDto>>> GetCauseOfLossCodes(
        [FromQuery] PerilCategory? perilCategory, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListCauseOfLossCodesQuery(perilCategory), cancellationToken);
        return Ok(result);
    }
}
