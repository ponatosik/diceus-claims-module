using ClaimsModule.Domain;

namespace ClaimsModule.Application;

/// <summary>Read-only repository over the seeded CauseOfLossCodes reference table (§5.6).</summary>
public interface ICauseOfLossCodeRepository
{
    Task<CauseOfLossCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CauseOfLossCode>> ListActiveAsync(PerilCategory? perilCategory, CancellationToken cancellationToken = default);
}
