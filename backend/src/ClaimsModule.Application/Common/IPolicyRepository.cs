using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common;

/// <summary>Read-only repository over the seeded simulated Policies table (§5.5/§9.10).</summary>
public interface IPolicyRepository
{
    Task<IReadOnlyList<Policy>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<Policy?> GetByIdAsync(Guid policyId, CancellationToken cancellationToken = default);
}
