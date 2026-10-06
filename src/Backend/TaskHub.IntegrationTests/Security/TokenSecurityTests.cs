using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using TaskHub.Application.Common;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Access tokens that were forged, altered, expired or issued for something else must not sign anyone in.
/// </summary>
public class TokenSecurityTests : IClassFixture<TaskHubApiFactory>
{
    private const string OtherSigningKey = "another-signing-key-that-the-server-does-not-know-0123456789";

    private readonly TaskHubApiFactory _factory;

    public TokenSecurityTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GenuineToken_IsAccepted()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.GetAsync(ApiRoutes.Me);

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenBuiltLikeAGenuineOne_IsAccepted()
    {
        // Proves the forged tokens below are refused for the reason under test, not because they are malformed.
        var user = await _factory.CreateUserAsync();
        var token = Forge().Create(TokenForge.AccessClaims(user.UserId, user.UserName, 1, Roles.User));

        var response = await _factory.WithToken(token).GetAsync(ApiRoutes.Me);

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.e30.")]
    [InlineData("null")]
    [InlineData("undefined")]
    public async Task MalformedToken_IsRejected(string token)
    {
        var session = _factory.Anonymous();
        session.Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");

        var response = await session.GetAsync(ApiRoutes.Me);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var token = Forge().Create(TokenForge.AccessClaims(user.UserId, user.UserName, 1, Roles.Admin), signingKey: OtherSigningKey);

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task TokenFromAnotherIssuer_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var token = Forge().Create(TokenForge.AccessClaims(user.UserId, user.UserName, 1, Roles.User), issuer: "SomeoneElse");

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task TokenForAnotherAudience_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var token = Forge().Create(TokenForge.AccessClaims(user.UserId, user.UserName, 1, Roles.User), audience: "AnotherApplication");

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var token = Forge().Create(
            TokenForge.AccessClaims(user.UserId, user.UserName, 1, Roles.User),
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddMinutes(-5));

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task TokenNotValidYet_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var token = Forge().Create(
            TokenForge.AccessClaims(user.UserId, user.UserName, 1, Roles.User),
            notBefore: DateTime.UtcNow.AddHours(1),
            expires: DateTime.UtcNow.AddHours(2));

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task UnsignedToken_IsRejected()
    {
        var user = await _factory.CreateUserAsync();

        await AssertRejectedAsync(TokenForge.StripSignature(user.Token!));
    }

    [Fact]
    public async Task TokenWithoutSignaturePart_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var parts = user.Token!.Split('.');

        await AssertRejectedAsync($"{parts[0]}.{parts[1]}.");
    }

    [Fact]
    public async Task TokenWithAnAdminRoleAddedToThePayload_IsRejected()
    {
        var user = await _factory.CreateUserAsync();

        var token = TokenForge.TamperPayload(user.Token!, payload => payload["role"] = new JsonArray("User", "Admin"));

        await AssertRejectedAsync(token);
        await AssertRejectedAsync(token, ApiRoutes.AdminUsers);
    }

    [Fact]
    public async Task TokenWithTheUserSwappedInThePayload_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var admin = await _factory.AdminAsync();

        var token = TokenForge.TamperPayload(user.Token!, payload => payload["nameid"] = admin.UserId.ToString());

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task TokenWithAnExtendedLifetimeInThePayload_IsRejected()
    {
        var user = await _factory.CreateUserAsync();

        var token = TokenForge.TamperPayload(user.Token!, payload => payload["exp"] = DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeSeconds());

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task TwoFactorStepToken_IsNotAnAccessToken()
    {
        // Signed with the right key, but marked as the token that only proves the first login step.
        var user = await _factory.CreateUserAsync();
        var claims = TokenForge.AccessClaims(user.UserId, user.UserName, 1, Roles.User);
        claims.Add(new Claim(AppClaimTypes.Purpose, "2fa"));

        await AssertRejectedAsync(Forge().Create(claims));
    }

    [Fact]
    public async Task TokenWithoutASessionVersion_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Role, Roles.Admin)
        };

        await AssertRejectedAsync(Forge().Create(claims));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(999)]
    [InlineData(-1)]
    public async Task TokenWithAnotherSessionVersion_IsRejected(int tokenVersion)
    {
        var user = await _factory.CreateUserAsync();
        var token = Forge().Create(TokenForge.AccessClaims(user.UserId, user.UserName, tokenVersion, Roles.User));

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task TokenOfAUserThatDoesNotExist_IsRejected()
    {
        var token = Forge().Create(TokenForge.AccessClaims(Guid.NewGuid(), "ghost", 0, Roles.Admin));

        await AssertRejectedAsync(token);
        await AssertRejectedAsync(token, ApiRoutes.AdminUsers);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("1 OR 1=1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task TokenWithAnInvalidUserId_IsRejected(string userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(AppClaimTypes.TokenVersion, "0"),
            new(ClaimTypes.Role, Roles.Admin)
        };

        await AssertRejectedAsync(Forge().Create(claims));
    }

    [Fact]
    public async Task TokenWithoutAUser_IsRejected()
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.TokenVersion, "0"),
            new(ClaimTypes.Role, Roles.Admin)
        };

        await AssertRejectedAsync(Forge().Create(claims));
    }

    [Fact]
    public async Task AccessTokenInTheQueryString_IsIgnored()
    {
        var user = await _factory.CreateUserAsync();

        var response = await _factory.Anonymous().GetAsync($"{ApiRoutes.Me}?access_token={user.Token}");

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessTokenInACookie_IsIgnored()
    {
        var user = await _factory.CreateUserAsync();
        var session = _factory.Anonymous();
        session.Client.DefaultRequestHeaders.Add("Cookie", $"access_token={user.Token}; Authorization=Bearer {user.Token}");

        var response = await session.GetAsync(ApiRoutes.Me);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BasicAuthentication_IsNotAccepted()
    {
        var user = await _factory.CreateUserAsync();
        var session = _factory.Anonymous();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.UserName}:{user.Password}"));
        session.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await session.GetAsync(ApiRoutes.Me);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenSentWithAnotherScheme_IsNotAccepted()
    {
        var user = await _factory.CreateUserAsync();
        var session = _factory.Anonymous();
        session.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", user.Token);

        var response = await session.GetAsync(ApiRoutes.Me);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RejectedToken_DoesNotRevealWhyInTheBody()
    {
        var token = Forge().Create(TokenForge.AccessClaims(Guid.NewGuid(), "ghost", 0, Roles.Admin));

        var response = await _factory.WithToken(token).GetAsync(ApiRoutes.Me);
        var body = await response.Content.ReadAsStringAsync();

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(string.Empty, body);
    }

    private TokenForge Forge()
    {
        return new TokenForge(_factory.Jwt);
    }

    private async Task AssertRejectedAsync(string token, string url = ApiRoutes.Me)
    {
        var response = await _factory.WithToken(token).GetAsync(url);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }
}
