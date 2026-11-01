using ClaimsModule.Infrastructure.BackgroundJobs;
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

        services.AddScoped<PostGLReserveChangeJob>();
        services.AddScoped<GLPostingFailureHandler>();

        return services;
    }

    /// <summary>
    /// Registers global job filters and recurring jobs — called once after the app is built, since
    /// Hangfire's recurring-job registration needs storage already configured. Each background-job
    /// task in this slice appends one line.
    /// </summary>
    public static void UseInfrastructure(this IServiceProvider services)
    {
        if (!GlobalJobFilters.Filters.Any(f => f.Instance is GLPostingFailureFilter))
        {
            GlobalJobFilters.Filters.Add(new GLPostingFailureFilter(services));
        }
    }
}
