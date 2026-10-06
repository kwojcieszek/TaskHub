using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Projects;

public class CreateProjectRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? GitUrl { get; set; }

    public Guid? ProjectGroupId { get; set; }
}
