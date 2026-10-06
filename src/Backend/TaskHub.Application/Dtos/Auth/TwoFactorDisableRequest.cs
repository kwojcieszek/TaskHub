using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class TwoFactorDisableRequest
{
    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}
