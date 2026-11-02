using ClaimsModule.API.Authentication;
using ClaimsModule.Application.Users;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ClaimsModule.API.Controllers;

public record DevTokenRequest(Guid UserId);

public record DevTokenResponse(string AccessToken, Guid UserId, string DisplayName, string Role);

/// <summary>
/// The self-hosted test token issuer (ADR-0007), amended by ADR-0010: mints a JWT for one of the three
/// fixed Demo Users, taking the role from the seeded user directory rather than the caller. Gated on
/// <c>DevAuth:Enabled</c>; returns 404 when disabled so it can never be mistaken for production auth.
/// </summary>
[ApiController]
[Route("api/dev/auth")]
public class DevAuthController(
    IOptions<DevAuthOptions> devAuthOptions,
    IOptions<JwtOptions> jwtOptions,
    IMediator mediator) : ControllerBase
{
    [HttpPost("token")]
    public async Task<ActionResult<DevTokenResponse>> CreateToken([FromBody] DevTokenRequest request, CancellationToken cancellationToken)
    {
        if (!devAuthOptions.Value.Enabled)
        {
            return NotFound();
        }

        var user = await mediator.Send(new GetDemoUserQuery(request.UserId), cancellationToken);
        if (user is null)
        {
            return BadRequest("UserId must be one of the seeded demo users.");
        }

        var token = TestTokenFactory.CreateToken(user.Role, user.UserId, jwtOptions.Value);
        return Ok(new DevTokenResponse(token, user.UserId, user.DisplayName, user.Role));
    }
}
