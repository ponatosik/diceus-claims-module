using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class LossEventConfiguration : IEntityTypeConfiguration<LossEvent>
{
    public void Configure(EntityTypeBuilder<LossEvent> builder)
    {
        builder.ToTable("LossEvents");
        builder.HasKey(e => e.LossEventId);

        builder.Property(e => e.LossEventId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.LossDate).IsRequired().HasPrecision(7);
        builder.Property(e => e.LossDescription).IsRequired().HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.LossLocation).HasMaxLength(500);
        builder.Property(e => e.CauseOfLossCode).IsRequired().HasMaxLength(50);
        builder.Property(e => e.EstimatedLossAmount).HasPrecision(19, 4);
        builder.Property(e => e.ReportDate).IsRequired().HasPrecision(7);
        builder.Property(e => e.PoliceReportNumber).HasMaxLength(100);

        builder.HasOne<Claim>().WithOne(c => c.LossEvent).HasForeignKey<LossEvent>(e => e.ClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CauseOfLossCode>().WithMany().HasForeignKey(e => e.CauseOfLossCode).HasPrincipalKey(c => c.Code).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantAudit();
    }
}
