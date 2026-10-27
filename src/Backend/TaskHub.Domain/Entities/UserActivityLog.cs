using TaskHub.Domain.Enums;

namespace TaskHub.Domain.Entities;

public class UserActivityLog
{
    public Guid Id { get; set; }

    public UserActivityType Type { get; set; } = UserActivityType.Request;

    // Empty for a login attempt that matched no account.
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string? IpAddress { get; set; }

    // Recorded for login attempts only.
    public string? UserAgent { get; set; }

    // Set on a login attempt that was rejected; a login without it succeeded.
    public LoginFailureReason? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
