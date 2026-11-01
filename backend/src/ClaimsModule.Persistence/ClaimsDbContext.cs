using System.Reflection;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    private static readonly MethodInfo ApplyTenantAuditQueryFilterMethod = typeof(ClaimsDbContext)
        .GetMethod(nameof(ApplyTenantAuditQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// The tenant every query on this context instance is scoped to. Defaults to the single fixed
    /// seed organisation (this is a greenfield, effectively single-tenant system today) — a future
    /// multi-tenant deployment would set this per request instead. Read by the combined soft-delete +
    /// tenant-isolation global query filter applied in <see cref="OnModelCreating"/> (NFR-EF-02,
    /// §16.2). Application code must never hardcode an organisation id (NFR-CONST-04); this property
    /// is the one place it's threaded through.
    /// </summary>
    public Guid CurrentOrganisationId { get; set; } = SeedData.OrganisationId;

    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimReserveComponent> ClaimReserveComponents => Set<ClaimReserveComponent>();
    public DbSet<ReserveHistory> ReserveHistoryEntries => Set<ReserveHistory>();
    public DbSet<LossEvent> LossEvents => Set<LossEvent>();
    public DbSet<ClaimParty> ClaimParties => Set<ClaimParty>();
    public DbSet<ClaimRiskObject> ClaimRiskObjects => Set<ClaimRiskObject>();
    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();
    public DbSet<ClaimAuditLog> ClaimAuditLogEntries => Set<ClaimAuditLog>();
    public DbSet<CauseOfLossCode> CauseOfLossCodes => Set<CauseOfLossCode>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);

        // Backs IClaimNumberGenerator (ADR-0001) — a single, never-resetting sequence read via
        // an atomic "NEXT VALUE FOR" call, never read-then-increment in application code.
        modelBuilder.HasSequence<long>("ClaimNumberSequence").StartsAt(1).IncrementsBy(1);

        // Applied centrally (not per-entity in IEntityTypeConfiguration<T>, which has no access to
        // `this`) so every AuditableEntity gets the same combined soft-delete + tenant-isolation
        // filter (NFR-EF-02) — reference/lookup entities (CauseOfLossCode, Policy) are deliberately
        // not AuditableEntity and so are untouched by this loop.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantAuditQueryFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantAuditQueryFilter<T>(ModelBuilder modelBuilder)
        where T : AuditableEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && e.OrganisationId == CurrentOrganisationId);
    }
}
