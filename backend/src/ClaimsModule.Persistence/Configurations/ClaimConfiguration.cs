using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");
        builder.HasKey(c => c.ClaimId);

        builder.Property(c => c.ClaimId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.ClaimNumber)
            .IsRequired()
            .HasMaxLength(50);
        builder.HasIndex(c => c.ClaimNumber).IsUnique();

        builder.Property(c => c.PolicyNumber).HasMaxLength(50);
        builder.Property(c => c.ClientName).HasMaxLength(255);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(c => c.Severity)
            .HasMaxLength(50)
            .HasConversion<string?>();

        builder.Property(c => c.ReportedDate)
            .IsRequired()
            .HasPrecision(7);

        builder.Property(c => c.ClosedAt).HasPrecision(7);
        builder.Property(c => c.ClosureReason).HasMaxLength(500);
        builder.Property(c => c.Notes).HasColumnType("NVARCHAR(MAX)");

        builder.Property(c => c.ReserveOverrideApproved).IsRequired().HasDefaultValue(false);

        builder.Property(c => c.RowVer).IsRowVersion();

        builder.HasOne<Policy>().WithMany().HasForeignKey(c => c.PolicyId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantAudit();
    }
}
