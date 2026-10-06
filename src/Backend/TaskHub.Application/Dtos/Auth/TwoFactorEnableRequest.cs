namespace TaskHub.Application.Dtos.Auth;

public class TwoFactorEnableRequest
{
    public string Code { get; set; } = string.Empty;
}
