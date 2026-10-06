namespace TaskHub.Application.Dtos.Projects;

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? GitUrl { get; set; }

    public Guid? ProjectGroupId { get; set; }
}
