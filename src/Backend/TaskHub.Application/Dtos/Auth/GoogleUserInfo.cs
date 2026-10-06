namespace TaskHub.Application.Dtos.Auth;

public class GoogleUserInfo
{
    public string Subject { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }
}
