using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskHub.Domain.Entities;

namespace TaskHub.Infrastructure.Persistence.Configurations;

public class UserActivityLogConfiguration : IEntityTypeConfiguration<UserActivityLog>
{
    public void Configure(EntityTypeBuilder<UserActivityLog> builder)
    {
        builder.ToTable("UserActivityLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.UserName).IsRequired().HasMaxLength(256);
        builder.Property(l => l.Action).IsRequired().HasMaxLength(200);
        builder.Property(l => l.HttpMethod).IsRequired().HasMaxLength(10);
        builder.Property(l => l.Path).IsRequired().HasMaxLength(500);
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.UserAgent).HasMaxLength(500);
        builder.Property(l => l.FailureReason).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(l => new { l.Type, l.CreatedAt });
        builder.HasIndex(l => l.UserId);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
