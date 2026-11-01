using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(configuration.GetConnectionString("HangfireDatabase")));
        services.AddHangfireServer();

        return services;
    }

    /// <summary>
    /// Registers global job filters and recurring jobs — called once after the app is built, since
    /// Hangfire's recurring-job registration needs storage already configured. Empty for now; each
    /// background-job task in this slice appends one line.
    /// </summary>
    public static void UseInfrastructure(this IServiceProvider services)
    {
    }
}
