using System.Net;
using TaskHub.Application.Dtos.Auth;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// A password somebody else knows (the published default, or one assigned by an administrator)
/// only opens the account endpoints, until the user replaces it.
/// </summary>
public class PasswordChangeRequiredTests : IClassFixture<TaskHubApiFactory>
{
    private const string Id = "11111111-2222-3333-4444-555555555555";

    private readonly TaskHubApiFactory _factory;

    public PasswordChangeRequiredTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string> BlockedEndpoints => new()
    {
        { "GET", "/v1/projects" },
        { "POST", "/v1/projects" },
        { "GET", $"/v1/projects/{Id}" },
        { "DELETE", $"/v1/projects/{Id}" },
        { "GET", "/v1/project-groups" },
        { "GET", "/v1/tasks" },
        { "GET", $"/v1/projects/{Id}/tasks" },
        { "GET", "/v1/users" },
        { "GET", "/v1/settings" },
        { "PUT", "/v1/settings/theme" },
        { "GET", "/v1/attachments/limits" },
        { "GET", "/v1/admin/users" },
        { "GET", "/v1/user-groups" },
        { "GET", "/v1/tokens" },
        { "POST", "/v1/tokens" },
        { "GET", $"/v1/projects/{Id}/teams" },
        { "GET", $"/v1/projects/{Id}/comments" },
        { "POST", $"/v1/projects/{Id}/comments" },

        // Addresses that merely mention the account area must not pass for it.
        { "GET", "/v1/projects?next=/v1/auth/" },
        { "GET", "/v1/settings/auth" },
        { "GET", "/v1/settings/%2Fapi%2Fv1%2Fauth%2Fme" },
        { "GET", "/v1/users?q=/v1/auth/me" },
        { "GET", "/v1/auth/../projects" },
        { "GET", "/v1/projects#/v1/auth/" }
    };

    [Theory]
    [MemberData(nameof(BlockedEndpoints))]
    public async Task AssignedPassword_DoesNotOpenTheApplication(string method, string url)
    {
        var user = await SignInWithAssignedPasswordAsync();
        var hasBody = method is "POST" or "PUT";

        var response = await user.SendAsync(new HttpMethod(method), url, hasBody ? new { name = "Blocked", value = "x" } : null);

        var errors = await response.ReadErrorsAsync(HttpStatusCode.Forbidden);
        Assert.Contains("change your password", errors[0]);
    }

    [Fact]
    public async Task AssignedPassword_OpensTheAccountEndpoints()
    {
        var user = await SignInWithAssignedPasswordAsync();

        var me = await user.GetAsync<UserDto>(ApiRoutes.Me);
        var logins = await user.GetAsync(ApiRoutes.MyLogins);

        Assert.True(me.MustChangePassword);
        await logins.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NothingIsCreatedWhileThePasswordChangeIsPending()
    {
        var user = await SignInWithAssignedPasswordAsync();

        var create = await user.PostAsync(ApiRoutes.Projects, new { name = "Created too early" });
        await create.ShouldBeAsync(HttpStatusCode.Forbidden);

        await ChangePasswordAsync(user, TaskHubApiFactory.UserPassword);

        var projects = await user.GetAsync<List<object>>(ApiRoutes.Projects);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task ReplacingThePassword_OpensTheApplication()
    {
        var user = await SignInWithAssignedPasswordAsync();

        var changed = await ChangePasswordAsync(user, TaskHubApiFactory.UserPassword);

        Assert.False(changed.User!.MustChangePassword);

        var projects = await user.GetAsync(ApiRoutes.Projects);
        await projects.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenIssuedBeforeTheChange_DoesNotOpenTheApplicationAfterwards()
    {
        var user = await SignInWithAssignedPasswordAsync();
        var pendingToken = user.Token!;

        await ChangePasswordAsync(user, TaskHubApiFactory.UserPassword);

        var response = await _factory.WithToken(pendingToken).GetAsync(ApiRoutes.Projects);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    [InlineData("        ")]
    public async Task ReplacementPassword_MustMeetThePasswordPolicy(string newPassword)
    {
        var user = await SignInWithAssignedPasswordAsync();

        var response = await user.PostAsync(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = newPassword });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var projects = await user.GetAsync(ApiRoutes.Projects);
        await projects.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReplacementPassword_MustDifferFromTheAssignedOne()
    {
        var user = await SignInWithAssignedPasswordAsync();

        var response = await user.PostAsync(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = user.Password });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var projects = await user.GetAsync(ApiRoutes.Projects);
        await projects.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangingThePassword_RequiresTheCurrentOne()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.PostAsync(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = "Wrong-Passw0rd-1", NewPassword = "Another-Passw0rd-1" });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);

        // The old password still signs in, the attempted one does not.
        var withOld = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        var withAttempted = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Another-Passw0rd-1" });

        await withOld.ShouldBeAsync(HttpStatusCode.OK);
        await withAttempted.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangingThePassword_CannotBeDoneForAnotherUser()
    {
        // The endpoint takes no user id; extra fields naming a victim must be ignored.
        var attacker = await _factory.CreateUserAsync("attacker");
        var victim = await _factory.CreateUserAsync("victim");

        var response = await attacker.PostAsync(ApiRoutes.Password, new
        {
            currentPassword = attacker.Password,
            newPassword = "Hijacked-Passw0rd-1",
            userId = victim.UserId,
            userName = victim.UserName
        });

        await response.ShouldBeAsync(HttpStatusCode.OK);

        var victimLogin = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = victim.UserName, Password = victim.Password });
        var hijackedLogin = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = victim.UserName, Password = "Hijacked-Passw0rd-1" });

        await victimLogin.ShouldBeAsync(HttpStatusCode.OK);
        await hijackedLogin.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DefaultAdministratorPassword_OnlyLetsTheAdministratorReplaceIt()
    {
        using var factory = new TaskHubApiFactory();
        var admin = await factory.LoginAsync(TaskHubApiFactory.AdminUserName, TaskHubApiFactory.InitialAdminPassword);

        var me = await admin.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.True(me.MustChangePassword);

        var users = await admin.GetAsync(ApiRoutes.AdminUsers);
        var security = await admin.GetAsync(ApiRoutes.AdminSecurity);
        await users.ShouldBeAsync(HttpStatusCode.Forbidden);
        await security.ShouldBeAsync(HttpStatusCode.Forbidden);

        // The default password is exempt from the policy; its replacement is not.
        var weak = await admin.PostAsync(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = TaskHubApiFactory.InitialAdminPassword, NewPassword = "admin2" });
        await weak.ReadErrorsAsync(HttpStatusCode.BadRequest);

        await ChangePasswordAsync(admin, TaskHubApiFactory.AdminPassword);

        var usersAfterwards = await admin.GetAsync(ApiRoutes.AdminUsers);
        await usersAfterwards.ShouldBeAsync(HttpStatusCode.OK);

        var withDefault = await factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = TaskHubApiFactory.AdminUserName, Password = TaskHubApiFactory.InitialAdminPassword });
        await withDefault.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    private async Task<ApiSession> SignInWithAssignedPasswordAsync()
    {
        var user = await _factory.CreatePendingUserAsync();
        var login = await _factory.LoginAsync(user);

        Assert.True(login.User!.MustChangePassword);

        return user;
    }

    private static async Task<LoginResponse> ChangePasswordAsync(ApiSession session, string newPassword)
    {
        var changed = await session.PostAsync<LoginResponse>(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = session.Password, NewPassword = newPassword });

        session.Password = newPassword;
        session.Authenticate(changed.AccessToken!);

        return changed;
    }
}
