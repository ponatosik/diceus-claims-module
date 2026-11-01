using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsModule.API.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Wires real JWT signature/issuer/audience/lifetime validation (ADR-0007) — nothing about
    /// token *validation* is faked, only the token *source* (the self-hosted dev issuer).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new InvalidOperationException(
                        "Missing \"Jwt\" configuration. The dev signing key lives only in " +
                        "appsettings.Development.json (ADR-0007) — a non-Development environment must " +
                        "supply its own Jwt:Issuer/Jwt:Audience/Jwt:SigningKey (or point at a real IdP).");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                };
            });

        return services;
    }
}
