using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimReserveComponentConfiguration : IEntityTypeConfiguration<ClaimReserveComponent>
{
    public void Configure(EntityTypeBuilder<ClaimReserveComponent> builder)
    {
        builder.ToTable("ClaimReserveComponents");
        builder.HasKey(c => c.ReserveComponentId);

        builder.Property(c => c.ReserveComponentId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.Component).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(c => c.CurrentAmount).IsRequired().HasPrecision(19, 4);
        builder.Property(c => c.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(c => c.Notes).HasColumnType("NVARCHAR(MAX)");

        builder.Property(c => c.RowVer).IsRowVersion();

        // Backstop against two concurrent first-time OpenReserveCommand requests for the same
        // component both reading "not found" and inserting a duplicate component row (Review Focus #5).
        builder.HasIndex(c => new { c.ClaimId, c.Component }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(c => c.Claim).WithMany().HasForeignKey(c => c.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantAudit();
    }
}
