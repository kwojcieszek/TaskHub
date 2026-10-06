using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Projects;

public class AddProjectMemberRequest
{
    public Guid UserId { get; set; }

    public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Viewer;
}
