namespace ClaimsModule.Application;

/// <summary>
/// Atomically generates the next claim number (ADR-0001). Implementations must guarantee uniqueness
/// under concurrent calls via a database-level atomic increment — never read-then-increment in
/// application code.
/// </summary>
public interface IClaimNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
