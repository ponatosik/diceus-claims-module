using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.Common;

/// <summary>Read-only lookup over the seeded Demo User directory (ADR-0010) — display names and roles for user ids.</summary>
public interface IUserDirectory
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>One batched lookup per response; ids with no directory entry are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken = default);

    /// <summary>Ids of users whose display name contains <paramref name="text"/> (case-insensitive) — backs the dashboard's handler text filter.</summary>
    Task<IReadOnlyList<Guid>> FindIdsByDisplayNameAsync(string text, CancellationToken cancellationToken = default);
}
