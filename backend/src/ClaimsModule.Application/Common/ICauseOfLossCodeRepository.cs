using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common;

/// <summary>Read-only repository over the seeded CauseOfLossCodes reference table (§5.6).</summary>
public interface ICauseOfLossCodeRepository
{
    Task<CauseOfLossCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Code → Name for display, including inactive codes (a claim may reference a since-deactivated code).</summary>
    Task<IReadOnlyDictionary<string, string>> GetNamesByCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CauseOfLossCode>> ListActiveAsync(PerilCategory? perilCategory, CancellationToken cancellationToken = default);
}
