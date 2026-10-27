namespace TaskHub.Application.Options;

public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public string ResetUrl { get; set; } = string.Empty;

    public int TokenLifetimeMinutes { get; set; } = 60;
}
