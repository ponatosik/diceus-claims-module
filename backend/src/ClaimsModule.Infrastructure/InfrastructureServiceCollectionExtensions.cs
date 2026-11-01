using Azure.Storage.Blobs;
using ClaimsModule.Application;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.Infrastructure.Storage;
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

        // Storage/client/recurring-job-manager stay registered regardless — only the actual worker
        // count is conditional, so no job ever gets dequeued/run when disabled. ApiFixture sets
        // Hangfire:StartServer=false so ApiIntegrationTests can enqueue and inspect real jobs
        // deterministically without a live server racing the assertions (or Respawn's resets).
        // Deliberately using the (IServiceProvider, BackgroundJobServerOptions) overload, not a
        // plain Action<BackgroundJobServerOptions> or an eagerly-evaluated `if` around the whole
        // AddHangfireServer() call — this callback runs when the hosted service actually starts
        // (after the host's final configuration, including WebApplicationFactory's test overrides,
        // is fully merged), whereas `configuration` here only reflects what's been added by the time
        // this extension method itself is called, which is too early to see those overrides.
        services.AddHangfireServer((serviceProvider, options) =>
        {
            var startServer = serviceProvider.GetRequiredService<IConfiguration>().GetValue("Hangfire:StartServer", true);
            if (!startServer)
            {
                // WorkerCount must be positive (Hangfire rejects 0), so disabling means pointing the
                // one required worker at a queue nothing is ever enqueued to, rather than a worker
                // count of zero.
                options.WorkerCount = 1;
                options.Queues = ["disabled-in-tests"];
            }
        });

        services.AddScoped<PostGLReserveChangeJob>();
        services.AddScoped<GLPostingFailureHandler>();
        services.AddScoped<OutboxDispatcherJob>();
        services.AddScoped<SlaMonitoringJob>();

        services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"));
        services.AddScoped<LocalFileSystemStorageService>();

        // Lazily resolved, like AddHangfireServer's options callback above — reads IConfiguration at
        // first resolution (well after WebApplicationFactory's test overrides merge in), not eagerly
        // here. BlobServiceClient itself is read the same way, as a singleton factory.
        // Pinned below the SDK's default (latest) service version — Testcontainers' pinned Azurite
        // image (see ApiFixture) lags newest Azure service API versions, and the default would 400
        // against it with "API version ... is not supported by Azurite."
        services.AddSingleton(sp => new BlobServiceClient(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("AzureBlobStorage"),
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_08_03)));
        services.AddScoped<AzureBlobStorageService>();
        services.AddScoped<IStorageService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            // BR-D-03: "If Azure Blob Storage is not configured, fall back to local filesystem" —
            // an empty/missing connection string IS "not configured", regardless of what
            // StorageProvider says, so a default appsettings.json (no real Azure credentials filled
            // in) falls back automatically instead of throwing on every upload/list request.
            var azureConfigured = !string.IsNullOrWhiteSpace(config.GetConnectionString("AzureBlobStorage"));
            var useLocal = config.GetValue("StorageProvider", "AzureBlob") == "LocalFileSystem" || !azureConfigured;
            return useLocal
                ? sp.GetRequiredService<LocalFileSystemStorageService>()
                : sp.GetRequiredService<AzureBlobStorageService>();
        });

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

        // Service-based API (IRecurringJobManager resolved from DI), not the static RecurringJob
        // helper — the static helper relies on JobStorage.Current already being set as a side
        // effect, which isn't guaranteed at this point under WebApplicationFactory's deferred host
        // startup; resolving the registered service avoids depending on that ordering at all.
        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();
        recurringJobManager.AddOrUpdate<OutboxDispatcherJob>("outbox-dispatcher", j => j.ExecuteAsync(CancellationToken.None), Cron.Minutely);
        recurringJobManager.AddOrUpdate<SlaMonitoringJob>("sla-monitoring", j => j.ExecuteAsync(CancellationToken.None), "*/15 * * * *");
    }
}
