using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(2048)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
