using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class TwoFactorVerifyRequest
{
    [Required]
    [StringLength(4096)]
    public string TwoFactorToken { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;
}
