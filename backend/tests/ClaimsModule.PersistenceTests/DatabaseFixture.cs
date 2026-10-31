using ClaimsModule.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;

namespace ClaimsModule.PersistenceTests;

/// <summary>
/// One SQL Server Testcontainer, started once per test assembly run (see <see cref="DatabaseCollection"/>),
/// per ADR-0009. Individual test classes call <see cref="ResetAsync"/> before each test to clear state
/// rather than relying on database-per-test or transaction rollback.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private SqlConnection? _respawnConnection;
    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(ConnectionString).Options;
        await using var dbContext = new ClaimsDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            _respawnConnection = new SqlConnection(ConnectionString);
            await _respawnConnection.OpenAsync();

            _respawner = await Respawner.CreateAsync(_respawnConnection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                // Migration-seeded reference data (§16.4) must survive a reset — only transactional/business
                // data created by a test should be cleared.
                TablesToIgnore = [new Respawn.Graph.Table("CauseOfLossCodes"), new Respawn.Graph.Table("Policies")],
            });
        }

        await _respawner.ResetAsync(_respawnConnection!);
    }

    public async Task DisposeAsync()
    {
        if (_respawnConnection is not null)
        {
            await _respawnConnection.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
