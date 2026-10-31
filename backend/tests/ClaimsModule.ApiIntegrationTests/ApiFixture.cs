using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.28.0").Build();

    private string _claimsConnectionString = string.Empty;

    public string HangfireConnectionString { get; private set; } = string.Empty;

    public string AzuriteConnectionString => _azuriteContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlContainer.StartAsync(), _azuriteContainer.StartAsync());

        _claimsConnectionString = _sqlContainer.GetConnectionString();
        HangfireConnectionString = await CreateHangfireStandInDatabaseAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClaimsDatabase"] = _claimsConnectionString,
            });
        });
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
