using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.OutboxMessageId);

        builder.Property(m => m.OutboxMessageId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(m => m.Type).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Payload).IsRequired().HasColumnType("NVARCHAR(MAX)");
        builder.Property(m => m.ProcessedAt).HasPrecision(7);

        // The dispatcher (Slice D) polls specifically for unprocessed rows.
        builder.HasIndex(m => m.ProcessedAt);

        builder.ConfigureTenantCreatedAt();
    }
}
