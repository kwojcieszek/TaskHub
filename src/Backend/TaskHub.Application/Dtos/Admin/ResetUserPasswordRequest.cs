using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Admin;

public class ResetUserPasswordRequest
{
    [Required]
    [StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
