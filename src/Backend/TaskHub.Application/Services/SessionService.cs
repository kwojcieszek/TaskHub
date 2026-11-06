using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;

namespace TaskHub.Application.Services;

public class SessionService : ISessionService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private Guid CurrentSession => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue(AppClaimTypes.SessionId), out var id) ? id : Guid.Empty;

    public SessionService(IApplicationDbContext dbContext, ICurrentUserService currentUser, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyCollection<SessionDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentSession = CurrentSession;

        var version = await _dbContext.UserSessions
            .Where(s => s.Id == currentSession && s.UserId == _currentUser.UserId)
            .Select(s => (int?)s.TokenVersion)
            .FirstOrDefaultAsync(cancellationToken);

        var sessions = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.ExpiresAt > now && s.TokenVersion == version)
            .OrderByDescending(s => s.LastUsedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new SessionDto
        {
            Id = s.Id,
            IsCurrent = s.Id == currentSession,
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            CreatedAt = DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc),
            LastUsedAt = DateTime.SpecifyKind(s.LastUsedAt, DateTimeKind.Utc),
            ExpiresAt = DateTime.SpecifyKind(s.ExpiresAt, DateTimeKind.Utc)
        }).ToList();
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var deleted = await _dbContext.UserSessions
            .Where(s => s.Id == sessionId && s.UserId == _currentUser.UserId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new NotFoundException("Session not found.");
        }
    }

    public async Task LogoutAsync(bool all, CancellationToken cancellationToken = default)
    {
        var current = CurrentSession;

        await _dbContext.UserSessions.Where(s => s.UserId == _currentUser.UserId && (all || s.Id == current)).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.TwoFactorChallenges.Where(c => c.UserId == _currentUser.UserId).ExecuteDeleteAsync(cancellationToken);
    }
}
