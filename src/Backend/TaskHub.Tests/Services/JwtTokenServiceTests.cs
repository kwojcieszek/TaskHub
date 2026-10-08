using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using TaskHub.Application.Common;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;
using TaskHub.Infrastructure.Services;

namespace TaskHub.Tests.Services;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "TaskHub.Tests",
        Audience = "TaskHub.Tests.Client",
        SigningKey = "unit-test-signing-key-that-is-long-enough-0123456789",
        AccessTokenMinutes = 30
    };

    private readonly JwtTokenService _tokens = new(Microsoft.Extensions.Options.Options.Create(Options));

    [Fact]
    public void AccessToken_CarriesTheUserRolesAndSessionVersion()
    {
        var user = NewUser();
        user.TokenVersion = 3;

        var (token, expiresAt) = _tokens.CreateAccessToken(user, new[] { Roles.Admin, Roles.User });
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(claim => claim.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("3", jwt.Claims.Single(claim => claim.Type == AppClaimTypes.TokenVersion).Value);
        Assert.Equal(new[] { Roles.Admin, Roles.User }, jwt.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value));
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == AppClaimTypes.Purpose);
        Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddMinutes(29), DateTimeOffset.UtcNow.AddMinutes(31));
    }

    [Fact]
    public void TwoFactorToken_RoundTripsTheUserIdAndIsMarkedWithItsPurpose()
    {
        var userId = Guid.NewGuid();

        var token = _tokens.CreateTwoFactorToken(userId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(userId, _tokens.ValidateTwoFactorToken(token));
        // The marker is what lets the API refuse this token as an access token.
        Assert.Contains(jwt.Claims, claim => claim.Type == AppClaimTypes.Purpose);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == AppClaimTypes.TokenVersion);
    }

    [Fact]
    public void ValidateTwoFactorToken_RejectsAnAccessToken()
    {
        var (accessToken, _) = _tokens.CreateAccessToken(NewUser(), Array.Empty<string>());

        Assert.Null(_tokens.ValidateTwoFactorToken(accessToken));
    }

    [Fact]
    public void ValidateTwoFactorToken_RejectsATokenSignedWithAnotherKey()
    {
        var otherOptions = new JwtOptions
        {
            Issuer = Options.Issuer,
            Audience = Options.Audience,
            SigningKey = "a-different-signing-key-that-is-long-enough-9876543210"
        };
        var foreignToken = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(otherOptions)).CreateTwoFactorToken(Guid.NewGuid());

        Assert.Null(_tokens.ValidateTwoFactorToken(foreignToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    public void ValidateTwoFactorToken_RejectsMalformedInput(string token)
    {
        Assert.Null(_tokens.ValidateTwoFactorToken(token));
    }

    private static User NewUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            UserName = "anna",
            Email = "anna@example.test",
            DisplayName = "Anna"
        };
    }
}
