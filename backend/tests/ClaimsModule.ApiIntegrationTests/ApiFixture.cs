using System.Net.Http.Headers;
using ClaimsModule.API.Authentication;
using ClaimsModule.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Respawn;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

namespace ClaimsModule.ApiIntegrationTests;

/// <summary>
/// Boots the full API through <see cref="WebApplicationFactory{TEntryPoint}"/> against real Testcontainers
/// infrastructure, per ADR-0009: one SQL Server container hosting two databases (the main ClaimsModule
/// database, and a stand-in for Hangfire's separate store per ADR-0004) plus one Azurite container.
/// Only the main database is wired into the running app today (nothing reads a Hangfire connection string
/// or a storage connection string yet) — the Hangfire/Azurite connection strings are exposed ahead of that
/// wiring landing, so this fixture doesn't need reshaping when it does.
/// </summary>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string HangfireStandInDatabaseName = "ClaimsModule_Hangfire";

    // Mirrors appsettings.json's Jwt section — kept in-memory here so tests never depend on a file
    // on disk, per this task's own instruction.
    private static readonly JwtOptions TestJwtOptions = new()
    {
        Issuer = "https://claims-module.local",
        Audience = "claims-module-api",
        SigningKey = "local-dev-only-signing-key-not-a-secret-32chars+",
    };

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.28.0").Build();

    private string _claimsConnectionString = string.Empty;
    private SqlConnection? _respawnConnection;
    private Respawner? _respawner;
    private SqlConnection? _hangfireRespawnConnection;
    private Respawner? _hangfireRespawner;

    public string HangfireConnectionString { get; private set; } = string.Empty;

    public string AzuriteConnectionString => _azuriteContainer.GetConnectionString();

    public string ClaimsConnectionString => _claimsConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlContainer.StartAsync(), _azuriteContainer.StartAsync());

        _claimsConnectionString = _sqlContainer.GetConnectionString();
        HangfireConnectionString = await CreateHangfireStandInDatabaseAsync();

        // The WebApplicationFactory-hosted app never migrates itself on startup (Program.cs is
        // deliberately thin — see CLAUDE.md), and this fixture is the first place in this project
        // that ever needs the main database's schema to actually exist. Mirrors
        // PersistenceTests/DatabaseFixture's own migrate-on-init step.
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(_claimsConnectionString).Options;
        await using var dbContext = new ClaimsDbContext(options);
        await dbContext.Database.MigrateAsync();

        // Forces the WebApplicationFactory host to build now rather than lazily on the first test's
        // first client — AddHangfire(...)'s UseSqlServerStorage call creates Hangfire's own schema in
        // the stand-in database as a side effect of host startup, and ResetAsync()'s Hangfire
        // Respawner (constructed on first use, below) needs at least one table to exist already.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClaimsDatabase"] = _claimsConnectionString,
                ["ConnectionStrings:HangfireDatabase"] = HangfireConnectionString,
                ["ConnectionStrings:AzureBlobStorage"] = AzuriteConnectionString,
                ["StorageProvider"] = "AzureBlob",
                // Storage/client/recurring-job-manager stay registered (jobs can genuinely be
                // enqueued against the real Hangfire stand-in database), but the actual background
                // processing server that dequeues and runs them is disabled — tests trigger jobs
                // directly and deterministically (per the background-jobs plan's own testing
                // decision), and a live server racing a test's own assertions, or racing Respawn's
                // reset of the same database between tests, would be a second source of races.
                ["Hangfire:StartServer"] = "false",
                ["DevAuth:Enabled"] = "true",
                [$"{JwtOptions.SectionName}:Issuer"] = TestJwtOptions.Issuer,
                [$"{JwtOptions.SectionName}:Audience"] = TestJwtOptions.Audience,
                [$"{JwtOptions.SectionName}:SigningKey"] = TestJwtOptions.SigningKey,
            });
        });
    }

    /// <summary>
    /// Mints a JWT in-process via <see cref="TestTokenFactory"/> (no HTTP round-trip to a separate
    /// issuer, per spec user story 26) and attaches it as a Bearer token on a fresh client.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string role, Guid? userId = null)
    {
        var client = CreateClient();
        var token = TestTokenFactory.CreateToken(role, userId ?? Guid.NewGuid(), TestJwtOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// A client against a separate host instance that reuses this fixture's containers but
    /// overrides <c>StorageProvider</c> — used to exercise the LocalFileSystem provider end-to-end
    /// (upload, static-file serving) without spinning up a second set of Testcontainers.
    /// </summary>
    public HttpClient CreateAuthenticatedClientWithStorageProvider(string storageProvider, string role, Guid? userId = null)
    {
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) =>
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?> { ["StorageProvider"] = storageProvider })));
        var client = factory.CreateClient();
        var token = TestTokenFactory.CreateToken(role, userId ?? Guid.NewGuid(), TestJwtOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Clears business/transactional data between tests sharing this one assembly-lifetime container
    /// (per ADR-0009's Respawn-based isolation, mirrored from <c>PersistenceTests/DatabaseFixture</c>).
    /// Migration-seeded reference data (§16.4) must survive a reset.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            _respawnConnection = new SqlConnection(_claimsConnectionString);
            await _respawnConnection.OpenAsync();

            _respawner = await Respawner.CreateAsync(_respawnConnection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                TablesToIgnore = [new Respawn.Graph.Table("CauseOfLossCodes"), new Respawn.Graph.Table("Policies"), new Respawn.Graph.Table("Users")],
            });
        }

        await _respawner.ResetAsync(_respawnConnection!);

        // Per ADR-0009: "both databases emptied before each test" — Hangfire's SQL storage creates
        // its own schema eagerly when AddHangfire(...) runs during host startup, so this is safe to
        // construct lazily on first use here, mirroring the main database's Respawner above.
        if (_hangfireRespawner is null)
        {
            _hangfireRespawnConnection = new SqlConnection(HangfireConnectionString);
            await _hangfireRespawnConnection.OpenAsync();

            _hangfireRespawner = await Respawner.CreateAsync(_hangfireRespawnConnection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
            });
        }

        await _hangfireRespawner.ResetAsync(_hangfireRespawnConnection!);
    }

    private async Task<string> CreateHangfireStandInDatabaseAsync()
    {
        await using var connection = new SqlConnection(_claimsConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID('{HangfireStandInDatabaseName}') IS NULL CREATE DATABASE [{HangfireStandInDatabaseName}];";
        await command.ExecuteNonQueryAsync();

        var builder = new SqlConnectionStringBuilder(_claimsConnectionString)
        {
            InitialCatalog = HangfireStandInDatabaseName,
        };
        return builder.ConnectionString;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_respawnConnection is not null)
        {
            await _respawnConnection.DisposeAsync();
        }

        if (_hangfireRespawnConnection is not null)
        {
            await _hangfireRespawnConnection.DisposeAsync();
        }

        await _sqlContainer.DisposeAsync();
        await _azuriteContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
