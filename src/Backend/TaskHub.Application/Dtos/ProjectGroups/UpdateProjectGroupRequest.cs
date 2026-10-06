namespace TaskHub.Application.Dtos.ProjectGroups;

public class UpdateProjectGroupRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
