using TaskHub.Domain.Enums;

namespace TaskHub.Domain.Entities;

public class LoginLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool Succeeded { get; set; }

    public LoginFailureReason? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
