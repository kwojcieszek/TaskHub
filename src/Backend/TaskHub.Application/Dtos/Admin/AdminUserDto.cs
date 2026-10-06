namespace TaskHub.Application.Dtos.Admin;

public class AdminUserDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool LockedOut { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}
