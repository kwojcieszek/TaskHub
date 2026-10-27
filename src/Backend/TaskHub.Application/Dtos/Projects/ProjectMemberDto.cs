namespace TaskHub.Application.Dtos.Projects;

public class ProjectMemberDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // The strongest role the user has, whether given directly or through a team.
    public string Role { get; set; } = string.Empty;

    // False when the user is in the project only through a team.
    public bool Direct { get; set; }

    // The teams of the project the user belongs to.
    public List<string> Teams { get; set; } = new();
}
