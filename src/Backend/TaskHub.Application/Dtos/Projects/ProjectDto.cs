namespace TaskHub.Application.Dtos.Projects;

public class ProjectDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? GitUrl { get; set; }

    public bool IsActive { get; set; }

    public Guid? ProjectGroupId { get; set; }

    public string? ProjectGroupName { get; set; }

    public Guid OwnerId { get; set; }

    public string OwnerDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int TaskCount { get; set; }

    public int SubTaskCount { get; set; }
}
