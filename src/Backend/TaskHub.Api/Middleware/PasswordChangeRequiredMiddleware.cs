using System.Text.Json;
using System.Text.RegularExpressions;
using TaskHub.Application.Common;

namespace TaskHub.Api.Middleware;

/// <summary>
/// Until a user replaces a password assigned to them (the default administrator's, or one set by an administrator),
/// only the account endpoints are available, so the assigned password cannot be used for anything else.
/// </summary>
public class PasswordChangeRequiredMiddleware
{
    // Only the account endpoints themselves (/v1/auth/...), not any path that merely contains "/auth/".
    private static readonly Regex AccountPathPattern = new("^/v[0-9.]+/auth/", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var mustChangePassword = context.User.HasClaim(claim => claim.Type == AppClaimTypes.PasswordChangeRequired);
        var path = context.Request.Path.Value ?? string.Empty;

        if (!mustChangePassword || AccountPathPattern.IsMatch(path))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { errors = new[] { "You must change your password before continuing." } });
        await context.Response.WriteAsync(payload);
    }
}
