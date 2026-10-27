namespace TaskHub.Application.Dtos.Projects;

public class ProjectTeamDto
{
    public Guid UserGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int MemberCount { get; set; }

    // Empty for a team that is offered but not yet added to the project.
    public string? Role { get; set; }
}
