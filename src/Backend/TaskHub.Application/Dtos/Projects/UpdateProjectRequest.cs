using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Projects;

public class UpdateProjectRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? GitUrl { get; set; }

    // Defaults to true so a client that does not send the field never deactivates a project by accident.
    public bool IsActive { get; set; } = true;
}
