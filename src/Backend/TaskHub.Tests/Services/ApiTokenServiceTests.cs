using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.ApiTokens;
using TaskHub.Application.Interfaces;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class ApiTokenServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IApiTokenService _tokens;

    public ApiTokenServiceTests()
    {
        _tokens = _host.Get<IApiTokenService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Create_ReturnsTheTokenOnceAndStoresOnlyItsHash()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);

        var created = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "  Script  ", ExpiresInDays = 30 });
        var stored = await _host.Db.ApiTokens.AsNoTracking().SingleAsync();

        Assert.StartsWith(ApiTokenFormat.Prefix, created.Token);
        Assert.True(created.Token.Length > 40);
        Assert.Equal("Script", created.Name);
        Assert.StartsWith(created.Prefix, created.Token);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.NotEqual(created.Token, stored.TokenHash);
        Assert.Null(stored.LastUsedAt);
    }

    [Fact]
    public async Task Create_MakesEveryTokenDifferent()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);

        var first = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Same" });
        var second = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Same" });

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Create_SetsTheExpiryFromTheNumberOfDays_OrNoneAtAll()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);

        var week = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Week", ExpiresInDays = 7 });
        var forever = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Forever", ExpiresInDays = null });

        Assert.NotNull(week.ExpiresAt);
        Assert.InRange(week.ExpiresAt.Value, DateTime.UtcNow.AddDays(7).AddMinutes(-1), DateTime.UtcNow.AddDays(7).AddMinutes(1));
        Assert.Equal(DateTimeKind.Utc, week.ExpiresAt.Value.Kind);
        Assert.False(week.Expired);
        Assert.Null(forever.ExpiresAt);
        Assert.False(forever.Expired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsABlankName(string name)
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _tokens.CreateAsync(new CreateApiTokenRequest { Name = name }));
        Assert.Empty(await _tokens.GetMineAsync());
    }

    [Fact]
    public async Task Create_StopsAtTheLimitPerUser()
    {
        var user = await _host.CreateUserAsync("anna");
        var other = await _host.CreateUserAsync("bartek");
        _host.SignIn(user);

        for (var index = 0; index < 20; index++)
        {
            await _tokens.CreateAsync(new CreateApiTokenRequest { Name = $"Token {index}" });
        }

        await Assert.ThrowsAsync<ConflictException>(() => _tokens.CreateAsync(new CreateApiTokenRequest { Name = "One too many" }));

        // The limit is per user: someone else is not affected by it.
        _host.SignIn(other);
        await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Unaffected" });
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyTheCallersTokens_NewestFirst_WithoutTheSecret()
    {
        var user = await _host.CreateUserAsync("anna");
        var other = await _host.CreateUserAsync("bartek");
        _host.SignIn(other);
        await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Foreign" });
        _host.SignIn(user);
        var older = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Older" });
        var newer = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Newer" });

        var mine = await _tokens.GetMineAsync();

        Assert.Equal(new[] { newer.Id, older.Id }, mine.Select(token => token.Id));
        Assert.All(mine, token => Assert.IsNotType<CreatedApiTokenDto>(token));
    }

    [Fact]
    public async Task Revoke_RemovesOwnToken_AndRefusesSomeoneElses()
    {
        var user = await _host.CreateUserAsync("anna");
        var other = await _host.CreateUserAsync("bartek");
        _host.SignIn(user);
        var mine = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Mine" });
        _host.SignIn(other);
        var theirs = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Theirs" });
        _host.SignIn(user);

        await _tokens.RevokeAsync(mine.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _tokens.RevokeAsync(theirs.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _tokens.RevokeAsync(mine.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _tokens.RevokeAsync(Guid.NewGuid()));
        Assert.Null(await _tokens.AuthenticateAsync(mine.Token));
        Assert.NotNull(await _tokens.AuthenticateAsync(theirs.Token));
    }

    [Fact]
    public async Task Authenticate_ReturnsTheUserWithTheirCurrentRoles_AndRecordsTheUse()
    {
        var user = await _host.CreateUserAsync("anna", Roles.User);
        _host.SignIn(user);
        var created = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Script" });

        var asUser = await _tokens.AuthenticateAsync(created.Token);
        await _host.Users.AddToRoleAsync(user, Roles.Admin);
        var asAdmin = await _tokens.AuthenticateAsync(created.Token);
        var stored = await _host.Db.ApiTokens.AsNoTracking().SingleAsync();

        Assert.NotNull(asUser);
        Assert.Equal(user.Id, asUser.UserId);
        Assert.Equal("anna", asUser.UserName);
        Assert.Equal(created.Id, asUser.TokenId);
        Assert.Equal(new[] { Roles.User }, asUser.Roles);
        Assert.False(asUser.MustChangePassword);
        Assert.NotNull(asAdmin);
        Assert.Contains(Roles.Admin, asAdmin.Roles);
        Assert.NotNull(stored.LastUsedAt);
    }

    [Fact]
    public async Task Authenticate_ReportsThatTheUserMustChangeThePassword()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);
        var created = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Script" });
        user.MustChangePassword = true;
        await _host.Users.UpdateAsync(user);

        var identity = await _tokens.AuthenticateAsync(created.Token);

        Assert.NotNull(identity);
        Assert.True(identity.MustChangePassword);
    }

    [Fact]
    public async Task Authenticate_RejectsWhatIsNotAValidToken()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);
        var created = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Script" });
        var stored = await _host.Db.ApiTokens.AsNoTracking().SingleAsync();

        var candidates = new[]
        {
            string.Empty,
            "   ",
            ApiTokenFormat.Prefix,
            created.Token[..^1],
            created.Token + "x",
            created.Token.ToUpperInvariant(),
            created.Token[ApiTokenFormat.Prefix.Length..],
            stored.TokenHash,
            ApiTokenFormat.Prefix + stored.TokenHash
        };

        foreach (var candidate in candidates)
        {
            Assert.Null(await _tokens.AuthenticateAsync(candidate));
        }
    }

    [Fact]
    public async Task Authenticate_RejectsAnExpiredToken_AndTheListMarksIt()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);
        var created = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Script", ExpiresInDays = 1 });

        var stored = await _host.Db.ApiTokens.SingleAsync();
        stored.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await _host.Db.SaveChangesAsync();

        Assert.Null(await _tokens.AuthenticateAsync(created.Token));
        Assert.True(Assert.Single(await _tokens.GetMineAsync()).Expired);
    }

    [Fact]
    public async Task Authenticate_RejectsTheTokenOfALockedUser_UntilTheLockEnds()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);
        var created = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Script" });

        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        await _host.Users.UpdateAsync(user);
        var whileLocked = await _tokens.AuthenticateAsync(created.Token);

        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _host.Users.UpdateAsync(user);
        var afterLock = await _tokens.AuthenticateAsync(created.Token);

        Assert.Null(whileLocked);
        Assert.NotNull(afterLock);
    }

    [Fact]
    public async Task DeletingTheUser_RemovesTheirTokens()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.SignIn(user);
        var created = await _tokens.CreateAsync(new CreateApiTokenRequest { Name = "Script" });

        await _host.Users.DeleteAsync(user);

        Assert.Null(await _tokens.AuthenticateAsync(created.Token));
        Assert.False(await _host.Db.ApiTokens.AsNoTracking().AnyAsync());
    }
}
