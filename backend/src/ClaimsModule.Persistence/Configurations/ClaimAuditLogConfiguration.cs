using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimAuditLogConfiguration : IEntityTypeConfiguration<ClaimAuditLog>
{
    public void Configure(EntityTypeBuilder<ClaimAuditLog> builder)
    {
        builder.ToTable("ClaimAuditLog");
        builder.HasKey(a => a.AuditLogId);

        builder.Property(a => a.AuditLogId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.EventType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Description).IsRequired().HasColumnType("NVARCHAR(MAX)");
        builder.Property(a => a.OldValue).HasColumnType("NVARCHAR(MAX)");
        builder.Property(a => a.NewValue).HasColumnType("NVARCHAR(MAX)");
        builder.Property(a => a.RelatedEntityType).HasMaxLength(100);

        builder.HasOne<Claim>().WithMany().HasForeignKey(a => a.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantCreatedAt();
    }
}
