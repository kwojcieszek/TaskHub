using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Audit;

public class UserActivityLogDto
{
    public Guid Id { get; set; }

    public UserActivityType Type { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public LoginFailureReason? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }
}
