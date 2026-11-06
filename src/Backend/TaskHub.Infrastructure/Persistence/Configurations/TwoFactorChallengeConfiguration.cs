using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskHub.Domain.Entities;

namespace TaskHub.Infrastructure.Persistence.Configurations;

public class TwoFactorChallengeConfiguration : IEntityTypeConfiguration<TwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<TwoFactorChallenge> builder)
    {
        builder.ToTable("TwoFactorChallenges");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TokenHash).HasMaxLength(64);
        builder.Property(s => s.SecurityStamp).HasMaxLength(256);
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
