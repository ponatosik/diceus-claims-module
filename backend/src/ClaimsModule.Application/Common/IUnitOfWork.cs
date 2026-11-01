namespace ClaimsModule.Application.Common;

/// <summary>
/// Owns the actual commit/transaction boundary shared by every repository against one scoped
/// DbContext (ADR-0003). Not injected into handlers directly — only repositories depend on it;
/// each repository's <c>CommitChangesAsync()</c> is a thin pass-through to this.
/// </summary>
public interface IUnitOfWork
{
    Task<int> CommitChangesAsync(CancellationToken cancellationToken = default);
}
