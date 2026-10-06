namespace TaskHub.Application.Dtos.Projects;

public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? GitUrl { get; set; }

    // Defaults to true so a client that does not send the field never deactivates a project by accident.
    public bool IsActive { get; set; } = true;
}
