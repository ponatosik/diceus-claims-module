using System.Security.Claims;
using ClaimsModule.Application.Common;
using Microsoft.AspNetCore.Http;

namespace ClaimsModule.API.Authentication;

public class HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Role => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
}
