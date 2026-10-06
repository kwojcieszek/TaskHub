using TaskHub.Application.Interfaces;

namespace TaskHub.Tests.Support;

/// <summary>
/// Stands in for the HTTP-based current user, so a test decides who is calling a service.
/// </summary>
public class FakeCurrentUserService : ICurrentUserService
{
    private readonly HashSet<string> _roles = new();

    public Guid UserId { get; private set; }

    public bool IsAuthenticated => UserId != Guid.Empty;

    public string? IpAddress => "127.0.0.1";

    public string? UserAgent => "tests";

    public bool IsInRole(string role)
    {
        return _roles.Contains(role);
    }

    public void SignIn(Guid userId, params string[] roles)
    {
        UserId = userId;
        _roles.Clear();

        foreach (var role in roles)
        {
            _roles.Add(role);
        }
    }
}
