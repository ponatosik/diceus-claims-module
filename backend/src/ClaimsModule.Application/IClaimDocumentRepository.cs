using ClaimsModule.Domain;

namespace ClaimsModule.Application;

/// <summary>Read-only repository over ClaimDocuments. No upload path exists yet — Slice E's job (§9.7).</summary>
public interface IClaimDocumentRepository
{
    Task<IReadOnlyList<ClaimDocument>> ListByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default);
}
