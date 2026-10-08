using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Projects;

public class AddProjectTeamRequest
{
    public Guid UserGroupId { get; set; }

    public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Viewer;
}
