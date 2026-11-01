using ClaimsModule.Application;
using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class PolicyRepository(ClaimsDbContext dbContext) : IPolicyRepository
{
    public async Task<IReadOnlyList<Policy>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        await dbContext.Policies
            .Where(p => p.PolicyNumber.Contains(query) || p.ClientName.Contains(query))
            .ToListAsync(cancellationToken);

    public Task<Policy?> GetByIdAsync(Guid policyId, CancellationToken cancellationToken = default) =>
        dbContext.Policies.FirstOrDefaultAsync(p => p.PolicyId == policyId, cancellationToken);
}
