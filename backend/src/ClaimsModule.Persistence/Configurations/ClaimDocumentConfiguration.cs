using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
{
    public void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        builder.ToTable("ClaimDocuments");
        builder.HasKey(d => d.ClaimDocumentId);

        builder.Property(d => d.ClaimDocumentId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.DocumentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.DocumentName).IsRequired().HasMaxLength(255);
        builder.Property(d => d.BlobPath).IsRequired().HasMaxLength(500);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.FileSizeBytes).IsRequired();
        builder.Property(d => d.UploadedAt).IsRequired().HasPrecision(7);
        builder.Property(d => d.Notes).HasMaxLength(500);

        builder.HasOne<Claim>().WithMany().HasForeignKey(d => d.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantAudit();
    }
}
