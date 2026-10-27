using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class GoogleTokenRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(1024)]
    public string? Password { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(6)]
    public string? Code { get; set; }
    [Required]
    [StringLength(8192)]
    public string IdToken { get; set; } = string.Empty;
}
