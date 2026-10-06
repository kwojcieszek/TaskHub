using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class ChangePasswordRequest
{
    [Required]
    [StringLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
