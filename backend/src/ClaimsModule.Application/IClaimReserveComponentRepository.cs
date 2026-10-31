using ClaimsModule.Domain;

namespace ClaimsModule.Application;

/// <summary>Repository for the ClaimReserveComponent aggregate root (ADR-0003). Query members are added as handlers need them.</summary>
public interface IClaimReserveComponentRepository
{
    void Add(ClaimReserveComponent component);
    Task CommitChangesAsync(CancellationToken cancellationToken = default);
}
