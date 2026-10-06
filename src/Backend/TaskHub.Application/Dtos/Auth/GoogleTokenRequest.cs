using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Auth;

public class GoogleTokenRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
