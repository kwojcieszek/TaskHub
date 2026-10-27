using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.ApiTokens;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Infrastructure.Persistence;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// API tokens a user creates on their account: they sign in as that user until they expire or are revoked,
/// follow the state of the account, and cannot be used to manage the account itself.
/// </summary>
public class ApiTokenTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public ApiTokenTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Token_SignsInAsItsOwner()
    {
        var user = await _factory.CreateUserAsync("tokenowner");
        var created = await CreateTokenAsync(user);
        var withToken = _factory.WithToken(created.Token);

        var me = await withToken.GetAsync<UserDto>(ApiRoutes.Me);
        var project = await withToken.CreateProjectAsync("Created with a token");

        Assert.StartsWith(ApiTokenFormat.Prefix, created.Token);
        Assert.Equal(user.UserId, me.Id);
        Assert.Equal(user.UserId, project.OwnerId);
    }

    [Fact]
    public async Task Token_IsShownOnlyOnce_AndStoredAsAHash()
    {
        var user = await _factory.CreateUserAsync("tokenonce");
        var created = await CreateTokenAsync(user, "Deploy");

        var list = await user.GetAsync(ApiRoutes.Tokens);
        var body = await list.Content.ReadAsStringAsync();
        var listed = Assert.Single(await list.ReadAsync<List<ApiTokenDto>>());

        Assert.DoesNotContain(created.Token, body);
        Assert.Equal("Deploy", listed.Name);
        Assert.Equal(created.Id, listed.Id);
        Assert.StartsWith(listed.Prefix, created.Token);
        Assert.True(listed.Prefix.Length < created.Token.Length / 2);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await database.ApiTokens.AsNoTracking().SingleAsync(token => token.Id == created.Id);

        Assert.NotEqual(created.Token, stored.TokenHash);
        Assert.DoesNotContain(created.Token[ApiTokenFormat.Prefix.Length..], stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
    }

    [Fact]
    public async Task Token_LivesAsLongAsAskedFor_OrForever()
    {
        var user = await _factory.CreateUserAsync("tokenlife");

        var forever = await CreateTokenAsync(user, "Forever", expiresInDays: null);
        var month = await CreateTokenAsync(user, "Month", expiresInDays: 30);
        var longest = await CreateTokenAsync(user, "Longest", expiresInDays: CreateApiTokenRequest.MaxLifetimeDays);

        Assert.Null(forever.ExpiresAt);
        Assert.False(forever.Expired);
        Assert.NotNull(month.ExpiresAt);
        Assert.InRange(month.ExpiresAt.Value.ToUniversalTime(), DateTime.UtcNow.AddDays(30).AddMinutes(-5), DateTime.UtcNow.AddDays(30).AddMinutes(5));
        Assert.NotNull(longest.ExpiresAt);

        var withForever = await _factory.WithToken(forever.Token).GetAsync(ApiRoutes.Me);
        await withForever.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CreateApiTokenRequest.MaxLifetimeDays + 1)]
    [InlineData(int.MaxValue)]
    public async Task Token_WithAnImpossibleLifetime_IsRefused(int expiresInDays)
    {
        var user = await _factory.CreateUserAsync("tokenbad");

        var response = await user.PostAsync(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = "Bad", ExpiresInDays = expiresInDays });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
        Assert.Empty(await user.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Token_WithoutAName_IsRefused(string name)
    {
        var user = await _factory.CreateUserAsync("tokennoname");

        var response = await user.PostAsync(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = name });
        var tooLong = await user.PostAsync(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = new string('a', 101) });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await tooLong.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var user = await _factory.CreateUserAsync("tokenexpired");
        var created = await CreateTokenAsync(user, expiresInDays: 1);
        var withToken = _factory.WithToken(created.Token);

        var before = await withToken.GetAsync(ApiRoutes.Me);
        await before.ShouldBeAsync(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await database.ApiTokens.SingleAsync(token => token.Id == created.Id);
            stored.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await database.SaveChangesAsync();
        }

        var after = await withToken.GetAsync(ApiRoutes.Me);
        var listed = Assert.Single(await user.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens));

        await after.ShouldBeAsync(HttpStatusCode.Unauthorized);
        Assert.True(listed.Expired);
    }

    [Fact]
    public async Task RevokedToken_StopsWorkingAtOnce()
    {
        var user = await _factory.CreateUserAsync("tokenrevoked");
        var kept = await CreateTokenAsync(user, "Kept");
        var revoked = await CreateTokenAsync(user, "Revoked");

        var revoke = await user.DeleteAsync(ApiRoutes.Token(revoked.Id));
        await revoke.ShouldBeAsync(HttpStatusCode.NoContent);

        var withRevoked = await _factory.WithToken(revoked.Token).GetAsync(ApiRoutes.Me);
        var withKept = await _factory.WithToken(kept.Token).GetAsync(ApiRoutes.Me);
        var revokeAgain = await user.DeleteAsync(ApiRoutes.Token(revoked.Id));
        var listed = Assert.Single(await user.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens));

        await withRevoked.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await withKept.ShouldBeAsync(HttpStatusCode.OK);
        await revokeAgain.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Equal(kept.Id, listed.Id);
    }

    [Fact]
    public async Task Token_OfAnotherUser_CannotBeSeenOrRevoked()
    {
        var owner = await _factory.CreateUserAsync("tokenvictim");
        var attacker = await _factory.CreateUserAsync("tokenattacker");
        var admin = await _factory.AdminAsync();
        var created = await CreateTokenAsync(owner);

        var attackerList = await attacker.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens);
        var adminList = await admin.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens);
        var attackerRevoke = await attacker.DeleteAsync(ApiRoutes.Token(created.Id));
        var missingRevoke = await attacker.DeleteAsync(ApiRoutes.Token(Guid.NewGuid()));

        Assert.Empty(attackerList);
        Assert.DoesNotContain(adminList, token => token.Id == created.Id);
        await attackerRevoke.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Equal(await missingRevoke.Content.ReadAsStringAsync(), await attackerRevoke.Content.ReadAsStringAsync());

        var stillWorks = await _factory.WithToken(created.Token).GetAsync(ApiRoutes.Me);
        await stillWorks.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_CannotManageTokensThePasswordOrTheSecondFactor()
    {
        // Otherwise a leaked token could mint others that outlive it, or take the account over.
        var user = await _factory.CreateUserAsync("tokenlimited");
        var created = await CreateTokenAsync(user);
        var withToken = _factory.WithToken(created.Token);

        var list = await withToken.GetAsync(ApiRoutes.Tokens);
        var create = await withToken.PostAsync(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = "Minted" });
        var revoke = await withToken.DeleteAsync(ApiRoutes.Token(created.Id));
        var password = await withToken.PostAsync(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = "Another-Passw0rd-1" });
        var twoFactorSetup = await withToken.PostAsync(ApiRoutes.TwoFactorSetup);
        var twoFactorDisable = await withToken.PostAsync(ApiRoutes.TwoFactorDisable, new TwoFactorDisableRequest { Password = user.Password });
        var googleLink = await withToken.PostAsync(ApiRoutes.GoogleLink, new GoogleTokenRequest { IdToken = "token" });

        await list.ShouldBeAsync(HttpStatusCode.Forbidden);
        await create.ShouldBeAsync(HttpStatusCode.Forbidden);
        await revoke.ShouldBeAsync(HttpStatusCode.Forbidden);
        await password.ShouldBeAsync(HttpStatusCode.Forbidden);
        await twoFactorSetup.ShouldBeAsync(HttpStatusCode.Forbidden);
        await twoFactorDisable.ShouldBeAsync(HttpStatusCode.Forbidden);
        await googleLink.ShouldBeAsync(HttpStatusCode.Forbidden);

        Assert.Single(await user.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens));

        var signIn = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        await signIn.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_HasTheRolesItsUserHasNow()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("tokenroles");
        var created = await CreateTokenAsync(user);
        var withToken = _factory.WithToken(created.Token);

        var asUser = await withToken.GetAsync(ApiRoutes.AdminUsers);

        var promote = await admin.PutAsync(ApiRoutes.AdminUserRoles(user.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.Admin, Roles.User } });
        await promote.ShouldBeAsync(HttpStatusCode.OK);
        var asAdmin = await withToken.GetAsync(ApiRoutes.AdminUsers);

        var demote = await admin.PutAsync(ApiRoutes.AdminUserRoles(user.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.User } });
        await demote.ShouldBeAsync(HttpStatusCode.OK);
        var asUserAgain = await withToken.GetAsync(ApiRoutes.AdminUsers);

        await asUser.ShouldBeAsync(HttpStatusCode.Forbidden);
        await asAdmin.ShouldBeAsync(HttpStatusCode.OK);
        await asUserAgain.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_OfALockedOrDeletedUser_IsRejected()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("tokenlocked");
        var created = await CreateTokenAsync(user);
        var withToken = _factory.WithToken(created.Token);

        var locking = await admin.PutAsync(ApiRoutes.AdminUserLockout(user.UserId), new SetUserLockoutRequest { LockedOut = true });
        await locking.ShouldBeAsync(HttpStatusCode.OK);
        var whileLocked = await withToken.GetAsync(ApiRoutes.Me);

        var unlocking = await admin.PutAsync(ApiRoutes.AdminUserLockout(user.UserId), new SetUserLockoutRequest { LockedOut = false });
        await unlocking.ShouldBeAsync(HttpStatusCode.OK);
        var afterUnlock = await withToken.GetAsync(ApiRoutes.Me);

        var deleting = await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));
        await deleting.ShouldBeAsync(HttpStatusCode.NoContent);
        var afterDelete = await withToken.GetAsync(ApiRoutes.Me);

        await whileLocked.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await afterUnlock.ShouldBeAsync(HttpStatusCode.OK);
        await afterDelete.ShouldBeAsync(HttpStatusCode.Unauthorized);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await database.ApiTokens.AnyAsync(token => token.Id == created.Id));
    }

    [Fact]
    public async Task Token_KeepsWorking_WhenThePasswordChanges()
    {
        // Tokens are for scripts that do not know the password; they end when they expire or are revoked.
        var user = await _factory.CreateUserAsync("tokenpassword");
        var created = await CreateTokenAsync(user);
        var oldSession = _factory.WithToken(user.Token!);

        var change = await user.PostAsync<LoginResponse>(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = "Another-Passw0rd-1" });
        user.Authenticate(change.AccessToken!);

        var withOldSession = await oldSession.GetAsync(ApiRoutes.Me);
        var withToken = await _factory.WithToken(created.Token).GetAsync(ApiRoutes.Me);

        await withOldSession.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await withToken.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownOrDamagedToken_IsRejected()
    {
        var user = await _factory.CreateUserAsync("tokenunknown");
        var created = await CreateTokenAsync(user);

        var candidates = new[]
        {
            ApiTokenFormat.Prefix,
            ApiTokenFormat.Prefix + "abc",
            created.Token[..^1],
            created.Token + "x",
            created.Token.ToUpperInvariant(),
            created.Token[ApiTokenFormat.Prefix.Length..],
            ApiTokenFormat.Prefix + user.Token,
            ApiTokenFormat.Prefix + "' OR '1'='1",
            ApiTokenFormat.Prefix + new string('a', 5000)
        };

        foreach (var candidate in candidates)
        {
            var session = _factory.Anonymous();
            session.Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {candidate}");

            var response = await session.GetAsync(ApiRoutes.Me);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"The token '{candidate[..Math.Min(candidate.Length, 40)]}' was answered with {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task UserWithAnAssignedPassword_CannotCreateTokens()
    {
        var pending = await _factory.CreatePendingUserAsync("tokenpending");
        await _factory.LoginAsync(pending);

        var create = await pending.PostAsync(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = "Early" });
        var list = await pending.GetAsync(ApiRoutes.Tokens);

        await create.ShouldBeAsync(HttpStatusCode.Forbidden);
        await list.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_OfAUserWhoMustChangeThePassword_OpensNothing()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("tokenreset");
        var created = await CreateTokenAsync(user);

        var reset = await admin.PutAsync(ApiRoutes.AdminUserPassword(user.UserId), new ResetUserPasswordRequest { NewPassword = TaskHubApiFactory.AssignedPassword });
        await reset.ShouldBeAsync(HttpStatusCode.OK);

        var projects = await _factory.WithToken(created.Token).GetAsync(ApiRoutes.Projects);

        await projects.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tokens_RequireSigningIn()
    {
        var anonymous = _factory.Anonymous();

        var list = await anonymous.GetAsync(ApiRoutes.Tokens);
        var create = await anonymous.PostAsync(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = "Anonymous" });
        var revoke = await anonymous.DeleteAsync(ApiRoutes.Token(Guid.NewGuid()));

        await list.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await create.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await revoke.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NumberOfTokens_IsLimitedPerUser()
    {
        const int limit = 20;
        var user = await _factory.CreateUserAsync("tokenmany");

        for (var index = 0; index < limit; index++)
        {
            await CreateTokenAsync(user, $"Token {index}");
        }

        var overLimit = await user.PostAsync(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = "One too many" });

        await overLimit.ShouldBeAsync(HttpStatusCode.Conflict);
        Assert.Equal(limit, (await user.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens)).Count);
    }

    [Fact]
    public async Task UsingAToken_RecordsWhenItWasLastUsed()
    {
        var user = await _factory.CreateUserAsync("tokenused");
        var created = await CreateTokenAsync(user);

        var unused = Assert.Single(await user.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens));
        var use = await _factory.WithToken(created.Token).GetAsync(ApiRoutes.Me);
        await use.ShouldBeAsync(HttpStatusCode.OK);
        var used = Assert.Single(await user.GetAsync<List<ApiTokenDto>>(ApiRoutes.Tokens));

        Assert.Null(unused.LastUsedAt);
        Assert.NotNull(used.LastUsedAt);
    }

    [Fact]
    public async Task Token_GivesNoMoreAccessThanItsOwnerHas()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var created = await CreateTokenAsync(scenario.Cast.Stranger);
        var withToken = _factory.WithToken(created.Token);

        var project = await withToken.GetAsync(ApiRoutes.Project(scenario.Project.Id));
        var task = await withToken.GetAsync(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id));
        var projects = await withToken.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);

        await project.ShouldBeAsync(HttpStatusCode.Forbidden);
        await task.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.DoesNotContain(projects, existing => existing.Id == scenario.Project.Id);
    }

    private static Task<CreatedApiTokenDto> CreateTokenAsync(ApiSession user, string name = "Script", int? expiresInDays = 30)
    {
        return user.PostAsync<CreatedApiTokenDto>(ApiRoutes.Tokens, new CreateApiTokenRequest { Name = name, ExpiresInDays = expiresInDays }, HttpStatusCode.Created);
    }
}
