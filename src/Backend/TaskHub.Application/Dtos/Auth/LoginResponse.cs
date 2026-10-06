namespace TaskHub.Application.Dtos.Auth;

public class LoginResponse
{
    public bool RequiresTwoFactor { get; set; }

    public string? TwoFactorToken { get; set; }

    public string? AccessToken { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public UserDto? User { get; set; }
}
