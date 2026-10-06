using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskHub.Application.Common;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;

namespace TaskHub.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private const string TwoFactorPurposeClaim = AppClaimTypes.Purpose;
    private const string TwoFactorPurposeValue = "2fa";

    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("displayName", user.DisplayName),
            new(AppClaimTypes.TokenVersion, user.TokenVersion.ToString(CultureInfo.InvariantCulture))
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = CreateToken(claims, expiresAt);

        return (token, expiresAt);
    }

    public string CreateTwoFactorToken(Guid userId)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(TwoFactorPurposeClaim, TwoFactorPurposeValue)
        };

        return CreateToken(claims, expiresAt);
    }

    public Guid? ValidateTwoFactorToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            var purpose = principal.FindFirstValue(TwoFactorPurposeClaim);

            if (purpose != TwoFactorPurposeValue)
            {
                return null;
            }

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out var id) ? id : null;
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            // Text that is not a token at all is reported as an argument error rather than a token error.
            return null;
        }
    }

    private string CreateToken(IEnumerable<Claim> claims, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
