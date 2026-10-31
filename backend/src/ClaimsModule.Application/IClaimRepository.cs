using ClaimsModule.Domain;

namespace ClaimsModule.Application;

/// <summary>Repository for the Claim aggregate root (ADR-0003). Query members are added as handlers need them.</summary>
public interface IClaimRepository
{
    void Add(Claim claim);
    Task CommitChangesAsync(CancellationToken cancellationToken = default);
}
