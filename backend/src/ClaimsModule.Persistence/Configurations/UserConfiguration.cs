using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.UserId);

        builder.Property(u => u.UserId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(255);
        builder.Property(u => u.Role).IsRequired().HasMaxLength(50);

        // ADR-0010's three fixed Demo Users — seeded at migration time (§16.4), never at startup.
        builder.HasData(
            new User { UserId = SeedData.DemoHandlerUserId, DisplayName = "Alex Morgan", Role = "handler" },
            new User { UserId = SeedData.DemoSupervisorUserId, DisplayName = "Priya Shah", Role = "supervisor" },
            new User { UserId = SeedData.DemoManagerUserId, DisplayName = "Daniel Okafor", Role = "manager" });
    }
}
