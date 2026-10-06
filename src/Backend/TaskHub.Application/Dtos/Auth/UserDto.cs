namespace TaskHub.Application.Dtos.Auth;

public class UserDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool TwoFactorEnabled { get; set; }

    public bool MustChangePassword { get; set; }

    public bool GoogleLinked { get; set; }

    public string? GoogleEmail { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}
