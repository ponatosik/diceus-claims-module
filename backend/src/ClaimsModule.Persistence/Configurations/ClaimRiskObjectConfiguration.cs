using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimRiskObjectConfiguration : IEntityTypeConfiguration<ClaimRiskObject>
{
    public void Configure(EntityTypeBuilder<ClaimRiskObject> builder)
    {
        builder.ToTable("ClaimRiskObjects");
        builder.HasKey(r => r.ClaimRiskObjectId);

        builder.Property(r => r.ClaimRiskObjectId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.AssetType).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(r => r.AssetDescription).IsRequired().HasMaxLength(500);
        builder.Property(r => r.DamageDescription).HasColumnType("NVARCHAR(MAX)");
        builder.Property(r => r.IsPrimary).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.AssetReference).HasMaxLength(255);

        builder.HasOne<Claim>().WithMany(c => c.RiskObjects).HasForeignKey(r => r.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantAudit();
    }
}
