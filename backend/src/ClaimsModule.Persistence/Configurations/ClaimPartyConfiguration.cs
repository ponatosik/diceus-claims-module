using ClaimsModule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimPartyConfiguration : IEntityTypeConfiguration<ClaimParty>
{
    public void Configure(EntityTypeBuilder<ClaimParty> builder)
    {
        builder.ToTable("ClaimParties");
        builder.HasKey(p => p.ClaimPartyId);

        builder.Property(p => p.ClaimPartyId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.PartyRole).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(p => p.PartyType).IsRequired().HasMaxLength(20).HasConversion<string>();
        builder.Property(p => p.FirstName).HasMaxLength(100);
        builder.Property(p => p.LastName).HasMaxLength(100);
        builder.Property(p => p.CompanyName).HasMaxLength(255);
        builder.Property(p => p.Email).HasMaxLength(255);
        builder.Property(p => p.Phone).HasMaxLength(50);
        builder.Property(p => p.Notes).HasColumnType("NVARCHAR(MAX)");
        builder.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne<Claim>().WithMany(c => c.Parties).HasForeignKey(p => p.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureTenantAudit();
    }
}
