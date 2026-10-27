using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.ApiTokens;

public class CreateApiTokenRequest
{
    public const int MaxLifetimeDays = 3650;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // How long the token stays valid. Leave empty for a token that never expires.
    [Range(1, MaxLifetimeDays)]
    public int? ExpiresInDays { get; set; }
}
