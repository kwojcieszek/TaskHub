namespace TaskHub.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? GitUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? ProjectGroupId { get; set; }

    public ProjectGroup? ProjectGroup { get; set; }

    public Guid OwnerId { get; set; }

    public User? Owner { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

    public ICollection<ProjectTeam> Teams { get; set; } = new List<ProjectTeam>();
}
