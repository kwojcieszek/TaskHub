using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskHub.Domain.Entities;

namespace TaskHub.Infrastructure.Persistence.Configurations;

public class TaskChangeConfiguration : IEntityTypeConfiguration<TaskChange>
{
    public void Configure(EntityTypeBuilder<TaskChange> b)
    {
        b.ToTable("TaskChanges");
        b.HasKey(c => c.Id);
        b.Property(c => c.ActorName).HasMaxLength(256);
        b.Property(c => c.Field).HasMaxLength(64);
        b.Property(c => c.OldValue).HasMaxLength(4500);
        b.Property(c => c.NewValue).HasMaxLength(4500);
        b.HasIndex(c => new { c.TaskItemId, c.ChangedAt });
        b.HasOne(c => c.TaskItem).WithMany().HasForeignKey(c => c.TaskItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
