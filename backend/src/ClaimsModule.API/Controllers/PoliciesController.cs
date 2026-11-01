using ClaimsModule.Application.Policies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/policies")]
public class PoliciesController(IMediator mediator) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<List<PolicySearchResultDto>>> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SearchPoliciesQuery(q), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/coverage")]
    public async Task<ActionResult<PolicyCoverageDto>> GetCoverage(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPolicyCoverageQuery(id), cancellationToken);
        return Ok(result);
    }
}
