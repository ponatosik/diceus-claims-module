using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>
/// Applies the shared §16.1/§16.2 conventions (tenant isolation, audit columns) consistently across
/// every <see cref="AuditableEntity"/> configuration, rather than repeating the same property setup
/// in each one. The combined soft-delete + tenant-isolation global query filter (NFR-EF-02) is NOT
/// applied here — it needs the owning <see cref="ClaimsDbContext"/> instance (for
/// <see cref="ClaimsDbContext.CurrentOrganisationId"/>), which an <see cref="IEntityTypeConfiguration{TEntity}"/>
/// doesn't receive. See <see cref="ClaimsDbContext.OnModelCreating"/>, which applies it centrally
/// across every <see cref="AuditableEntity"/>-derived type after these configurations run.
/// </summary>
public static class AuditableEntityConfigurationExtensions
{
    public static void ConfigureTenantAudit<T>(this EntityTypeBuilder<T> builder)
        where T : AuditableEntity
    {
        builder.Property(e => e.OrganisationId).IsRequired();

        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedAt).HasPrecision(7);

        builder.Property(e => e.CreatedAt).IsRequired().HasPrecision(7);
        builder.Property(e => e.UpdatedAt).HasPrecision(7);
    }

    public static void ConfigureTenantCreatedAt<T>(this EntityTypeBuilder<T> builder)
        where T : AppendOnlyEntity
    {
        builder.Property(e => e.OrganisationId).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired().HasPrecision(7);
    }
}
