using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.ProjectGroups;

public class UpdateProjectGroupRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
}
