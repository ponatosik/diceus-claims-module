using ClaimsModule.Application;
using ClaimsModule.Application.Common;

namespace ClaimsModule.Persistence;

public class UnitOfWork(ClaimsDbContext dbContext) : IUnitOfWork
{
    public Task<int> CommitChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
