using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;

namespace TaskHub.Application.Services;

public class SessionService(IApplicationDbContext db, ICurrentUserService currentUser, IHttpContextAccessor http) : ISessionService
{
    private Guid CurrentSession => Guid.TryParse(http.HttpContext?.User.FindFirstValue(AppClaimTypes.SessionId), out var id) ? id : Guid.Empty;

    public async Task<IReadOnlyCollection<SessionDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var version = await db.UserSessions.Where(s => s.Id == CurrentSession && s.UserId == currentUser.UserId)
            .Select(s => (int?)s.TokenVersion).FirstOrDefaultAsync(cancellationToken);
        var sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == currentUser.UserId && s.ExpiresAt > now && s.TokenVersion == version)
            .OrderByDescending(s => s.LastUsedAt).ToListAsync(cancellationToken);
        return sessions.Select(s => new SessionDto {
            Id = s.Id, IsCurrent = s.Id == CurrentSession, IpAddress = s.IpAddress, UserAgent = s.UserAgent,
            CreatedAt = DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc),
            LastUsedAt = DateTime.SpecifyKind(s.LastUsedAt, DateTimeKind.Utc),
            ExpiresAt = DateTime.SpecifyKind(s.ExpiresAt, DateTimeKind.Utc)
        }).ToList();
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var deleted = await db.UserSessions.Where(s => s.Id == sessionId && s.UserId == currentUser.UserId).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0) throw new NotFoundException("Session not found.");
    }

    public async Task LogoutAsync(bool all, CancellationToken cancellationToken = default)
    {
        var current = CurrentSession;
        await db.UserSessions.Where(s => s.UserId == currentUser.UserId && (all || s.Id == current)).ExecuteDeleteAsync(cancellationToken);
        await db.TwoFactorChallenges.Where(c => c.UserId == currentUser.UserId).ExecuteDeleteAsync(cancellationToken);
    }
}
