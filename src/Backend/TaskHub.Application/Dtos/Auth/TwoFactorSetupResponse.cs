namespace TaskHub.Application.Dtos.Auth;

public class TwoFactorSetupResponse
{
    public string SharedKey { get; set; } = string.Empty;

    public string AuthenticatorUri { get; set; } = string.Empty;
}
