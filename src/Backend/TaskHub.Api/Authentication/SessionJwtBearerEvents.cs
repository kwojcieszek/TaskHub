using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Domain.Entities;

namespace TaskHub.Api.Authentication;

/// <summary>
/// Checks every access token against the current state of its user, so that deleting or locking a user,
/// changing their roles or resetting their password takes effect at once instead of when the token expires.
/// </summary>
public class SessionJwtBearerEvents : JwtBearerEvents
{
    private readonly UserManager<ApplicationUser> _userManager;

    public SessionJwtBearerEvents(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
    }
}
