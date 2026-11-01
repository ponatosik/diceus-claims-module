namespace ClaimsModule.Application.Common;

/// <summary>
/// Identity of the caller making the current request. Both members are nullable — this must stay
/// safe to call from contexts with no authenticated caller at all (e.g. Slice D's background jobs),
/// not just this slice's always-<c>[Authorize]</c>d HTTP endpoints.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? Role { get; }
}
