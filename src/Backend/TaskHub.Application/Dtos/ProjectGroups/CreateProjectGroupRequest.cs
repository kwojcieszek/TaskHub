namespace TaskHub.Application.Dtos.ProjectGroups;

public class CreateProjectGroupRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
