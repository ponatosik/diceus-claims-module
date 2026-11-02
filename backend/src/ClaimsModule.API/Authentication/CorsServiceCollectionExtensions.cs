using Microsoft.AspNetCore.Cors.Infrastructure;

namespace ClaimsModule.API.Authentication;

/// <summary>Spec 006 story 1: browser origins allowed to call the API (the deployed Angular frontend).</summary>
public class FrontendCorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public static class CorsServiceCollectionExtensions
{
    /// <summary>
    /// Default CORS policy over <see cref="FrontendCorsOptions.AllowedOrigins"/> only — never a wildcard,
    /// and no credentials mode (auth is a Bearer header). Bound through DI's IConfiguration, the final
    /// merged configuration, so WebApplicationFactory test overrides apply (see Program.cs's static-files note).
    /// </summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<FrontendCorsOptions>().BindConfiguration(FrontendCorsOptions.SectionName);
        services.AddOptions<CorsOptions>()
            .Configure<Microsoft.Extensions.Options.IOptions<FrontendCorsOptions>>((options, frontendCors) =>
                options.AddDefaultPolicy(policy => policy
                    .WithOrigins(frontendCors.Value.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));
        return services;
    }
}
