namespace TaskHub.Application.Options;

public class SecurityOptions
{
    public const string SectionName = "Security";

    public bool LockoutEnabled { get; set; } = true;

    public int MaxFailedAccessAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;
}
