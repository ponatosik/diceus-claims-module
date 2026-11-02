using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

public class CauseOfLossCodeRepository(ClaimsDbContext dbContext) : ICauseOfLossCodeRepository
{
    public Task<CauseOfLossCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        dbContext.CauseOfLossCodes.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> GetNamesByCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default)
    {
        var distinctCodes = codes.Distinct().ToList();
        return await dbContext.CauseOfLossCodes.AsNoTracking()
            .Where(c => distinctCodes.Contains(c.Code))
            .ToDictionaryAsync(c => c.Code, c => c.Name, cancellationToken);
    }

    public async Task<IReadOnlyList<CauseOfLossCode>> ListActiveAsync(PerilCategory? perilCategory, CancellationToken cancellationToken = default)
    {
        var query = dbContext.CauseOfLossCodes.Where(c => c.IsActive);

        if (perilCategory is { } category)
        {
            query = query.Where(c => c.PerilCategory == category);
        }

        return await query.OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
    }
}
