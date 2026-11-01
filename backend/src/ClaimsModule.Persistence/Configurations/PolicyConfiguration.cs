using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");
        builder.HasKey(p => p.PolicyId);

        builder.Property(p => p.PolicyId).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.PolicyNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(p => p.PolicyNumber).IsUnique();

        builder.Property(p => p.ClientName).IsRequired().HasMaxLength(255);
        builder.Property(p => p.EffectiveDate).IsRequired();
        builder.Property(p => p.ExpirationDate).IsRequired();
        builder.Property(p => p.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(p => p.CoverageTypes).IsRequired().HasMaxLength(500);

        // §5.5 — seeded at migration time (§16.4), never at application startup.
        builder.HasData(
            new Policy { PolicyId = Guid.Parse("00000000-0000-0000-0002-000000000001"), PolicyNumber = "POL-2024-001001", ClientName = "Meridian Transport LLC", EffectiveDate = new DateOnly(2024, 1, 1), ExpirationDate = new DateOnly(2026, 12, 31), Status = PolicyStatus.Active, CoverageTypes = "Vehicle,Cargo" },
            new Policy { PolicyId = Guid.Parse("00000000-0000-0000-0002-000000000002"), PolicyNumber = "POL-2024-001002", ClientName = "Harborview Properties Inc", EffectiveDate = new DateOnly(2024, 6, 1), ExpirationDate = new DateOnly(2026, 5, 31), Status = PolicyStatus.Active, CoverageTypes = "Property,Liability" },
            new Policy { PolicyId = Guid.Parse("00000000-0000-0000-0002-000000000003"), PolicyNumber = "POL-2025-002001", ClientName = "Coastal Builders Group", EffectiveDate = new DateOnly(2025, 3, 1), ExpirationDate = new DateOnly(2027, 2, 28), Status = PolicyStatus.Active, CoverageTypes = "Property,Equipment" },
            new Policy { PolicyId = Guid.Parse("00000000-0000-0000-0002-000000000004"), PolicyNumber = "POL-2025-002002", ClientName = "Stanton Medical Group", EffectiveDate = new DateOnly(2025, 1, 1), ExpirationDate = new DateOnly(2026, 12, 31), Status = PolicyStatus.Active, CoverageTypes = "Liability,Vehicle" },
            new Policy { PolicyId = Guid.Parse("00000000-0000-0000-0002-000000000005"), PolicyNumber = "POL-2023-000099", ClientName = "Archived Corp", EffectiveDate = new DateOnly(2020, 1, 1), ExpirationDate = new DateOnly(2021, 12, 31), Status = PolicyStatus.Expired, CoverageTypes = "Property" }
        );
    }
}
