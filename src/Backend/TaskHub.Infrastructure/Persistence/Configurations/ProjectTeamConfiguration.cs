using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskHub.Domain.Entities;

namespace TaskHub.Infrastructure.Persistence.Configurations;

public class ProjectTeamConfiguration : IEntityTypeConfiguration<ProjectTeam>
{
    public void Configure(EntityTypeBuilder<ProjectTeam> builder)
    {
        builder.ToTable("ProjectTeams");

        builder.HasKey(t => t.Id);

        builder.HasIndex(t => new { t.ProjectId, t.UserGroupId }).IsUnique();

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Teams)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.UserGroup)
            .WithMany()
            .HasForeignKey(t => t.UserGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
