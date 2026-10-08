using TaskHub.Domain.Enums;

namespace TaskHub.Domain.Entities;

/// <summary>
/// A user group given access to a project: every member of the group gets the role, for as long as they are in it.
/// </summary>
public class ProjectTeam
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid UserGroupId { get; set; }

    public UserGroup? UserGroup { get; set; }

    public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Viewer;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
