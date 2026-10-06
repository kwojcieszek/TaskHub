namespace TaskHub.Application.Dtos.Projects;

public class ProjectMemberDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
