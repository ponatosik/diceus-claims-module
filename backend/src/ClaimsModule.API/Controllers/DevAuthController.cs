using ClaimsModule.API.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ClaimsModule.API.Controllers;

public record DevTokenRequest(string Role, Guid? UserId = null);

public record DevTokenResponse(string AccessToken);

/// <summary>
/// The self-hosted test token issuer ADR-0007 calls for — mints a JWT for one of the three roles
/// using the exact same signing logic <see cref="TestTokenFactory"/> gives ApiIntegrationTests
/// in-process. Scoped strictly to Development/Testing; returns 404 everywhere else so it can
/// never be mistaken for production-appropriate auth.
/// </summary>
[ApiController]
[Route("api/dev/auth")]
public class DevAuthController(IHostEnvironment environment, IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private static readonly string[] ValidRoles = ["handler", "supervisor", "manager"];

    [HttpPost("token")]
    public ActionResult<DevTokenResponse> CreateToken([FromBody] DevTokenRequest request)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        if (!ValidRoles.Contains(request.Role))
        {
            return BadRequest($"Role must be one of: {string.Join(", ", ValidRoles)}.");
        }

        var token = TestTokenFactory.CreateToken(request.Role, request.UserId ?? Guid.NewGuid(), jwtOptions.Value);
        return Ok(new DevTokenResponse(token));
    }
}
