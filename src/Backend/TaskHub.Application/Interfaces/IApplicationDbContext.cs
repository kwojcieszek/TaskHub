using Microsoft.EntityFrameworkCore;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ProjectGroup> ProjectGroups { get; }

    DbSet<Project> Projects { get; }

    DbSet<ProjectMember> ProjectMembers { get; }

    DbSet<ProjectTeam> ProjectTeams { get; }

    DbSet<TaskItem> Tasks { get; }

    DbSet<Attachment> Attachments { get; }

    DbSet<Comment> Comments { get; }

    DbSet<UserGroup> UserGroups { get; }

    DbSet<UserGroupMember> UserGroupMembers { get; }

    DbSet<UserSetting> UserSettings { get; }

    DbSet<UserActivityLog> UserActivityLogs { get; }

    DbSet<SystemSetting> SystemSettings { get; }

    DbSet<ApiToken> ApiTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
