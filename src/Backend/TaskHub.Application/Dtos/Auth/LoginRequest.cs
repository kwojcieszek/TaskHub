using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class LoginRequest
{
    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}
