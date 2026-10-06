namespace TaskHub.Application.Dtos.Security;

public class SecuritySettingsDto
{
    public bool LockoutEnabled { get; set; }

    public int MaxFailedAccessAttempts { get; set; }

    public int LockoutMinutes { get; set; }
}
