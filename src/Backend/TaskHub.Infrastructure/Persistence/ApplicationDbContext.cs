using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<User, Role, Guid>, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUser;
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<ProjectGroup> ProjectGroups => Set<ProjectGroup>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<ProjectTeam> ProjectTeams => Set<ProjectTeam>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<UserGroup> UserGroups => Set<UserGroup>();

    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();

    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    public DbSet<UserActivityLog> UserActivityLogs => Set<UserActivityLog>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<TaskChange> TaskChanges => Set<TaskChange>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries<TaskItem>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList();
        if (entries.Count > 0)
        {
            var actorId = _currentUser?.IsAuthenticated == true ? _currentUser.UserId : (Guid?)null;
            var actor = actorId.HasValue ? await Users.AsNoTracking().Where(u => u.Id == actorId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken) : null;
            var fields = new HashSet<string> { "Title", "Description", "DueDate", "Status", "Priority", "AssignedUserId", "ParentTaskId" };
            foreach (var entry in entries)
            {
                var task = entry.Entity;
                if (entry.State == EntityState.Added)
                    AddChange(task, "Created", null, task.Title, actorId, actor);
                else
                    foreach (var property in entry.Properties.Where(p => fields.Contains(p.Metadata.Name) && p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
                        AddChange(task, property.Metadata.Name, Format(property.OriginalValue), Format(property.CurrentValue), actorId, actor);
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AddChange(TaskItem task, string field, string? oldValue, string? newValue, Guid? actorId, string? actor)
        => TaskChanges.Add(new TaskChange { Id = Guid.NewGuid(), TaskItemId = task.Id, ProjectId = task.ProjectId,
            Field = field, OldValue = oldValue, NewValue = newValue, UserId = actorId, ActorName = actor ?? "System", ChangedAt = DateTime.UtcNow });

    private static string? Format(object? value) => value switch {
        null => null,
        DateTime date => date.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
    };

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<User>().ToTable("Users");
        builder.Entity<Role>().ToTable("Roles");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
