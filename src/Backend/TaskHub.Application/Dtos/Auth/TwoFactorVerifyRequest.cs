namespace TaskHub.Application.Dtos.Auth;

public class TwoFactorVerifyRequest
{
    public string TwoFactorToken { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
