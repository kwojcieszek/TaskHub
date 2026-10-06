using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<ProjectGroup> ProjectGroups => Set<ProjectGroup>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<UserGroup> UserGroups => Set<UserGroup>();

    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();

    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();

    public DbSet<UserActivityLog> UserActivityLogs => Set<UserActivityLog>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
