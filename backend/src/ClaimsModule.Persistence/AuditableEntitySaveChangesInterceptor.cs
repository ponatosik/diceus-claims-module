using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClaimsModule.Persistence;

/// <summary>
/// Stamps the shared audit/tenant columns (§16.1) on every <see cref="AuditableEntity"/>/<see cref="AppendOnlyEntity"/>
/// automatically on <c>SaveChanges</c>, so handlers never have to remember to set them themselves (found missing
/// entirely while re-checking Slice A — see this task's own notes in the implementation plan).
/// </summary>
public class AuditableEntitySaveChangesInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        StampAuditColumns(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        StampAuditColumns(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampAuditColumns(DbContext? context)
    {
        if (context is not ClaimsDbContext claimsDbContext)
        {
            return;
        }

        foreach (var entry in claimsDbContext.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case AuditableEntity auditableEntity when entry.State == EntityState.Added:
                    auditableEntity.OrganisationId = claimsDbContext.CurrentOrganisationId;
                    auditableEntity.CreatedAt = DateTimeOffset.UtcNow;
                    auditableEntity.UserCreated = currentUserService.UserId;
                    break;
                case AuditableEntity auditableEntity when entry.State == EntityState.Modified:
                    auditableEntity.UpdatedAt = DateTimeOffset.UtcNow;
                    auditableEntity.UserModified = currentUserService.UserId;
                    break;
                case AppendOnlyEntity appendOnlyEntity when entry.State == EntityState.Added:
                    appendOnlyEntity.OrganisationId = claimsDbContext.CurrentOrganisationId;
                    // Only stamp if the caller hasn't already assigned one — AuditLogService assigns
                    // its own strictly-monotonic CreatedAt (see its own comment) so that multiple
                    // ClaimAuditLog rows written within one SaveChanges call never tie; calling
                    // UtcNow() fresh per entity here, as this used to do unconditionally, can return
                    // the same instant twice in a tight loop on coarser clock resolutions.
                    if (appendOnlyEntity.CreatedAt == default)
                    {
                        appendOnlyEntity.CreatedAt = DateTimeOffset.UtcNow;
                    }

                    break;
            }
        }
    }
}
