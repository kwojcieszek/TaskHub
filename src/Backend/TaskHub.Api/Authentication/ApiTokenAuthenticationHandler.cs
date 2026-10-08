using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TaskHub.Application.Common;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Authentication;

/// <summary>
/// Signs a request in with an API token a user created on their account. The token is looked up on every
/// request, so revoking it, its expiry, and deleting or locking the user take effect at once.
/// </summary>
public class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BearerPrefix = "Bearer ";

    private readonly IApiTokenService _apiTokenService;

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiTokenService apiTokenService)
        : base(options, logger, encoder)
    {
        _apiTokenService = apiTokenService;
    }

    /// <summary>Whether the request carries an API token rather than a session token.</summary>
    public static bool CarriesApiToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();

        return header.StartsWith(BearerPrefix + ApiTokenFormat.Prefix, StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!CarriesApiToken(Request))
        {
            return AuthenticateResult.NoResult();
        }

        var token = Request.Headers.Authorization.ToString()[BearerPrefix.Length..].Trim();
        var identity = await _apiTokenService.AuthenticateAsync(token, Context.RequestAborted);

        if (identity is null)
        {
            return AuthenticateResult.Fail("The API token is invalid, expired or revoked.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.UserId.ToString()),
            new(ClaimTypes.Name, identity.UserName),
            new(AppClaimTypes.ApiTokenId, identity.TokenId.ToString())
        };

        claims.AddRange(identity.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (identity.MustChangePassword)
        {
            claims.Add(new Claim(AppClaimTypes.PasswordChangeRequired, bool.TrueString));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
