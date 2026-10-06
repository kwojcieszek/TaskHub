using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class RegisterRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}
