using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Domain.Entities;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Authentication;

/// <summary>
/// Checks every access token against the current state of its user, so that deleting or locking a user,
/// changing their roles or resetting their password takes effect at once instead of when the token expires.
/// </summary>
public class SessionJwtBearerEvents : JwtBearerEvents
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _dbContext;

    public SessionJwtBearerEvents(UserManager<User> userManager, IApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;

        // The token handed out between the password and the 2FA code is signed with the same key; it must not sign anyone in.
        if (principal is null || principal.HasClaim(claim => claim.Type == AppClaimTypes.Purpose))
        {
            context.Fail("The token is not an access token.");
            return;
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Fail("The token has no user.");
            return;
        }

        var user = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.TokenVersion, u.LockoutEnd, u.MustChangePassword })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null)
        {
            context.Fail("The user no longer exists.");
            return;
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            context.Fail("The user is locked out.");
            return;
        }

        var tokenVersion = principal.FindFirstValue(AppClaimTypes.TokenVersion);

        if (tokenVersion != user.TokenVersion.ToString(CultureInfo.InvariantCulture))
        {
            context.Fail("The session has been revoked.");
            return;
        }

        if (user.MustChangePassword && principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(AppClaimTypes.PasswordChangeRequired, bool.TrueString));
        }

        if (!Guid.TryParse(principal.FindFirstValue(AppClaimTypes.SessionId), out var sessionId))
        {
            context.Fail("The token has no active session.");
            return;
        }
        var now = DateTime.UtcNow;
        var session = await _dbContext.UserSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.TokenVersion == user.TokenVersion && s.ExpiresAt > now, context.HttpContext.RequestAborted);
        if (session is null)
        {
            context.Fail("The session has ended.");
            return;
        }
        if (session.LastUsedAt < now.AddMinutes(-5))
            await _dbContext.UserSessions.Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(update => update.SetProperty(s => s.LastUsedAt, now), context.HttpContext.RequestAborted);
    }
}
