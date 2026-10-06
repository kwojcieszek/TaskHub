using System.Net;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Auth;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// A token that was valid a moment ago stops working as soon as the account behind it changes:
/// deleted, locked, given other roles or another password.
/// </summary>
public class SessionRevocationTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public SessionRevocationTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeletedUser_LosesAccessAtOnce()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        var delete = await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var me = await user.GetAsync(ApiRoutes.Me);
        var projects = await user.GetAsync(ApiRoutes.Projects);
        var create = await user.PostAsync(ApiRoutes.Projects, new { name = "After deletion" });

        await me.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await projects.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await create.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletedUser_CannotSignInAgain()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();
        await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));

        var login = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        await login.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LockedUser_LosesAccessAtOnce_AndGetsItBackWhenUnlocked()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        var locked = await admin.PutAsync<AdminUserDto>(ApiRoutes.AdminUserLockout(user.UserId), new SetUserLockoutRequest { LockedOut = true });
        Assert.True(locked.LockedOut);

        var whileLocked = await user.GetAsync(ApiRoutes.Me);
        await whileLocked.ShouldBeAsync(HttpStatusCode.Unauthorized);

        var login = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        await login.ShouldBeAsync(HttpStatusCode.Unauthorized);

        var unlocked = await admin.PutAsync<AdminUserDto>(ApiRoutes.AdminUserLockout(user.UserId), new SetUserLockoutRequest { LockedOut = false });
        Assert.False(unlocked.LockedOut);

        var afterUnlock = await user.GetAsync(ApiRoutes.Me);
        await afterUnlock.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangingRoles_EndsTheSessionsOfTheUser()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        await admin.PutAsync<AdminUserDto>(ApiRoutes.AdminUserRoles(user.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.Admin, Roles.User } });

        // The old token says "User" only; it must not keep working, and it must not be upgraded silently either.
        var withOldToken = await user.GetAsync(ApiRoutes.Me);
        var adminWithOldToken = await user.GetAsync(ApiRoutes.AdminUsers);

        await withOldToken.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await adminWithOldToken.ShouldBeAsync(HttpStatusCode.Unauthorized);

        var login = await _factory.LoginAsync(user);
        Assert.Contains(Roles.Admin, login.User!.Roles);

        var adminWithNewToken = await user.GetAsync(ApiRoutes.AdminUsers);
        await adminWithNewToken.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DemotedAdministrator_LosesAdminAccessAtOnce()
    {
        var admin = await _factory.AdminAsync();
        var demoted = await _factory.CreateAdminAsync();

        var before = await demoted.GetAsync(ApiRoutes.AdminUsers);
        await before.ShouldBeAsync(HttpStatusCode.OK);

        await admin.PutAsync<AdminUserDto>(ApiRoutes.AdminUserRoles(demoted.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.User } });

        var withOldToken = await demoted.GetAsync(ApiRoutes.AdminUsers);
        await withOldToken.ShouldBeAsync(HttpStatusCode.Unauthorized);

        await _factory.LoginAsync(demoted);

        var withNewToken = await demoted.GetAsync(ApiRoutes.AdminUsers);
        var ordinaryAccess = await demoted.GetAsync(ApiRoutes.Projects);

        await withNewToken.ShouldBeAsync(HttpStatusCode.Forbidden);
        await ordinaryAccess.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserWithoutAnyRole_CanStillOnlyDoWhatAnOrdinaryUserCan()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        await admin.PutAsync<AdminUserDto>(ApiRoutes.AdminUserRoles(user.UserId), new UpdateUserRolesRequest { Roles = Array.Empty<string>() });
        await _factory.LoginAsync(user);

        var adminAccess = await user.GetAsync(ApiRoutes.AdminUsers);

        await adminAccess.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PasswordResetByAnAdministrator_EndsTheSessionsAndTheOldPassword()
    {
        const string resetPassword = "Reset-Passw0rd-1";

        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();
        var oldPassword = user.Password;

        var reset = await admin.PutAsync(ApiRoutes.AdminUserPassword(user.UserId), new ResetUserPasswordRequest { NewPassword = resetPassword });
        await reset.ShouldBeAsync(HttpStatusCode.OK);

        var withOldToken = await user.GetAsync(ApiRoutes.Me);
        await withOldToken.ShouldBeAsync(HttpStatusCode.Unauthorized);

        var withOldPassword = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = oldPassword });
        await withOldPassword.ShouldBeAsync(HttpStatusCode.Unauthorized);

        user.Password = resetPassword;
        var login = await _factory.LoginAsync(user);

        // The administrator knows the new password, so it only lets the user in to replace it.
        Assert.True(login.User!.MustChangePassword);

        var projects = await user.GetAsync(ApiRoutes.Projects);
        await projects.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangingOwnPassword_EndsEveryOtherSession()
    {
        const string newPassword = "Changed-Passw0rd-1";

        var user = await _factory.CreateUserAsync();
        var oldPassword = user.Password;
        var otherDevice = await _factory.LoginAsync(user.UserName, user.Password);

        var changed = await user.PostAsync<LoginResponse>(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = oldPassword, NewPassword = newPassword });
        var oldToken = user.Token!;
        user.Authenticate(changed.AccessToken!);

        var onOtherDevice = await otherDevice.GetAsync(ApiRoutes.Me);
        var withOldToken = await _factory.WithToken(oldToken).GetAsync(ApiRoutes.Me);
        var withNewToken = await user.GetAsync(ApiRoutes.Me);

        await onOtherDevice.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await withOldToken.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await withNewToken.ShouldBeAsync(HttpStatusCode.OK);

        var withOldPassword = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = oldPassword });
        var withNewPassword = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = newPassword });

        await withOldPassword.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await withNewPassword.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RevokingOneUser_LeavesOtherUsersSignedIn()
    {
        var admin = await _factory.AdminAsync();
        var removed = await _factory.CreateUserAsync("removed");
        var bystander = await _factory.CreateUserAsync("bystander");

        await admin.DeleteAsync(ApiRoutes.AdminUser(removed.UserId));

        var response = await bystander.GetAsync(ApiRoutes.Me);

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemovedProjectMember_LosesAccessToTheProjectAtOnce()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var editor = scenario.Cast.Editor;

        var before = await editor.GetAsync(ApiRoutes.Project(scenario.Project.Id));
        await before.ShouldBeAsync(HttpStatusCode.OK);

        var remove = await scenario.Cast.Owner.DeleteAsync(ApiRoutes.ProjectMember(scenario.Project.Id, editor.UserId));
        await remove.ShouldBeAsync(HttpStatusCode.NoContent);

        var project = await editor.GetAsync(ApiRoutes.Project(scenario.Project.Id));
        var tasks = await editor.GetAsync(ApiRoutes.Tasks(scenario.Project.Id));
        var download = await editor.GetAsync(ApiRoutes.AttachmentContent(scenario.Project.Id, scenario.LeafTask.Id, scenario.Attachment.Id));
        var edit = await editor.DeleteAsync(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id));

        await project.ShouldBeAsync(HttpStatusCode.Forbidden);
        await tasks.ShouldBeAsync(HttpStatusCode.Forbidden);
        await download.ShouldBeAsync(HttpStatusCode.Forbidden);
        await edit.ShouldBeAsync(HttpStatusCode.Forbidden);
    }
}
