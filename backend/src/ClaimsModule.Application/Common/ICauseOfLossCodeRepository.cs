using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common;

/// <summary>Read-only repository over the seeded CauseOfLossCodes reference table (§5.6).</summary>
public interface ICauseOfLossCodeRepository
{
    Task<CauseOfLossCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CauseOfLossCode>> ListActiveAsync(PerilCategory? perilCategory, CancellationToken cancellationToken = default);
}
