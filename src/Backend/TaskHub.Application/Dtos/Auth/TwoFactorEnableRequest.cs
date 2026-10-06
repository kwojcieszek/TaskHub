using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class TwoFactorEnableRequest
{
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;
}
