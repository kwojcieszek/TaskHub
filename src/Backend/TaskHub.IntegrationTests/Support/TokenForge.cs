using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.Tokens;
using TaskHub.Application.Common;
using TaskHub.Application.Options;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Builds access tokens the API never issued, the way an attacker would try to.
/// </summary>
public sealed class TokenForge
{
    private readonly JwtOptions _options;

    public TokenForge(JwtOptions options)
    {
        _options = options;
    }

    public string SigningKey => _options.SigningKey;

    /// <summary>The claims a genuine access token of the given user carries.</summary>
    public static List<Claim> AccessClaims(Guid userId, string userName, int tokenVersion, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new(AppClaimTypes.TokenVersion, tokenVersion.ToString(CultureInfo.InvariantCulture))
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }

    public string Create(
        IEnumerable<Claim> claims,
        string? signingKey = null,
        string? issuer = null,
        string? audience = null,
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? _options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer ?? _options.Issuer,
            audience: audience ?? _options.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires ?? DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Rewrites the payload of a genuine token and keeps its original signature.</summary>
    public static string TamperPayload(string token, Action<JsonObject> change)
    {
        var parts = token.Split('.');
        var payload = JsonNode.Parse(Base64UrlEncoder.Decode(parts[1]))!.AsObject();

        change(payload);

        return $"{parts[0]}.{Base64UrlEncoder.Encode(payload.ToJsonString())}.{parts[2]}";
    }

    /// <summary>Declares the token unsigned ("alg": "none") and drops the signature.</summary>
    public static string StripSignature(string token)
    {
        var parts = token.Split('.');
        var header = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new { alg = "none", typ = "JWT" }));

        return $"{header}.{parts[1]}.";
    }
}
