using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

public class PolicyRepository(ClaimsDbContext dbContext) : IPolicyRepository
{
    public async Task<IReadOnlyList<Policy>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        await dbContext.Policies
            .Where(p => p.PolicyNumber.Contains(query) || p.ClientName.Contains(query))
            .ToListAsync(cancellationToken);

    public Task<Policy?> GetByIdAsync(Guid policyId, CancellationToken cancellationToken = default) =>
        dbContext.Policies.FirstOrDefaultAsync(p => p.PolicyId == policyId, cancellationToken);
}
