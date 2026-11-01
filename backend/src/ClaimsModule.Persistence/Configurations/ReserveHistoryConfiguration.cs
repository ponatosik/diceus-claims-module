using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ReserveHistoryConfiguration : IEntityTypeConfiguration<ReserveHistory>
{
    public void Configure(EntityTypeBuilder<ReserveHistory> builder)
    {
        builder.ToTable("ReserveHistory");
        builder.HasKey(h => h.ReserveHistoryId);

        builder.Property(h => h.ReserveHistoryId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(h => h.TransactionType).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(h => h.Amount).IsRequired().HasPrecision(19, 4);
        builder.Property(h => h.PreviousBalance).IsRequired().HasPrecision(19, 4);
        builder.Property(h => h.NewBalance).IsRequired().HasPrecision(19, 4);
        builder.Property(h => h.ApprovalStatus).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(h => h.ApprovedAt).HasPrecision(7);
        builder.Property(h => h.RejectedAt).HasPrecision(7);
        builder.Property(h => h.RejectionReason).HasColumnType("NVARCHAR(MAX)");
        builder.Property(h => h.ChangeReason).IsRequired().HasMaxLength(500);
        builder.Property(h => h.PostingStatus).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(h => h.PostingJobId).HasMaxLength(100);

        builder.Property(h => h.IdempotencyKey).IsRequired().HasMaxLength(200);
        // DB-level unique constraint backing the GL posting job's idempotency guarantee (ADR-0005) —
        // a defensive backstop alongside the application-level check-before-write.
        builder.HasIndex(h => h.IdempotencyKey).IsUnique();

        builder.Property(h => h.RowVer).IsRowVersion();

        builder.HasOne<ClaimReserveComponent>().WithMany().HasForeignKey(h => h.ReserveComponentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Claim>().WithMany().HasForeignKey(h => h.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantCreatedAt();
    }
}
