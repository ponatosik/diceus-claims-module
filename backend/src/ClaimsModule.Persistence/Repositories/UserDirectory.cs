using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

public class UserDirectory(ClaimsDbContext dbContext) : IUserDirectory
{
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.DisplayName, cancellationToken);
    }

    // SQL Server's default collation is case-insensitive, so Contains() already matches case-insensitively.
    public async Task<IReadOnlyList<Guid>> FindIdsByDisplayNameAsync(string text, CancellationToken cancellationToken = default) =>
        await dbContext.Users.AsNoTracking()
            .Where(u => u.DisplayName.Contains(text))
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);
}
