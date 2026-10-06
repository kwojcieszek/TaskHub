namespace TaskHub.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }

    bool IsAuthenticated { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    bool IsInRole(string role);
}
