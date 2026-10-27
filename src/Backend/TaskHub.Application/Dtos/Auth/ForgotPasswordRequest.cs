using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
