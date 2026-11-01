using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

/// <summary>
/// Reads the next value from the <c>ClaimNumberSequence</c> SQL SEQUENCE object via an atomic
/// <c>NEXT VALUE FOR</c> call (ADR-0001). The sequence never resets — the year in the rendered
/// claim number is just the creation year, not tied to the sequence's own value.
/// </summary>
/// <remarks>
/// Uses a plain ADO.NET scalar command rather than <c>Database.SqlQuery&lt;T&gt;</c>: EF Core wraps
/// that API's SQL in a derived-table subquery for composability, and SQL Server forbids
/// <c>NEXT VALUE FOR</c> inside one ("not allowed in ... sub-queries, ... derived tables").
/// </remarks>
public class SqlSequenceClaimNumberGenerator(ClaimsDbContext dbContext) : IClaimNumberGenerator
{
    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT NEXT VALUE FOR ClaimNumberSequence";

            var result = await ((SqlCommand)command).ExecuteScalarAsync(cancellationToken);
            var sequenceValue = (long)result!;

            var year = DateTimeOffset.UtcNow.Year;
            return $"CLM-{year}-{sequenceValue:D7}";
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }
}
