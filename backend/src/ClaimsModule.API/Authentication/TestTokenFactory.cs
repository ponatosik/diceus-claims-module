using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsModule.API.Authentication;

/// <summary>
/// Mints JWTs signed with the same key/issuer/audience the running API validates against (ADR-0007).
/// Used by the Development/Testing-only token-issuing surface and called directly, in-process, by
/// <see cref="ApiIntegrationTests.ApiFixture"/> — no HTTP round-trip to a separate issuer (spec user
/// story 26).
/// </summary>
public static class TestTokenFactory
{
    public static string CreateToken(string role, Guid userId, JwtOptions options, TimeSpan? lifetime = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
