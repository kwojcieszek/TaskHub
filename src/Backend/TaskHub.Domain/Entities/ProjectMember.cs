using TaskHub.Domain.Enums;

namespace TaskHub.Domain.Entities;

public class ProjectMember
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Viewer;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
