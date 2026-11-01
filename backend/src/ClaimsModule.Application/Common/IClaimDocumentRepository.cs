using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common;

/// <summary>Repository for the ClaimDocument aggregate (ADR-0003, §9.7).</summary>
public interface IClaimDocumentRepository
{
    void Add(ClaimDocument document);

    Task CommitChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimDocument>> ListByClaimIdAsync(Guid claimId, CancellationToken cancellationToken = default);
}
