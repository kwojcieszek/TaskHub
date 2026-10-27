using TaskHub.Application.Dtos.Auth;

namespace TaskHub.Application.Interfaces;

public interface ISessionService
{
    Task<IReadOnlyCollection<SessionDto>> GetMineAsync(CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task LogoutAsync(bool all, CancellationToken cancellationToken = default);
}
