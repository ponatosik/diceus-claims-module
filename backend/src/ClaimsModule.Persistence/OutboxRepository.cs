using ClaimsModule.Application;
using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class OutboxRepository(ClaimsDbContext dbContext, IUnitOfWork unitOfWork) : IOutboxRepository
{
    public Task CommitChangesAsync(CancellationToken cancellationToken = default) =>
        unitOfWork.CommitChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> ListUnprocessedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
}
