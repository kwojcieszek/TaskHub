using System.Net;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Integration;

/// <summary>
/// The life of a user account as an administrator manages it: created, signed in, changed, grouped, deleted.
/// </summary>
public class UserManagementTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public UserManagementTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatedUser_AppearsInTheListAsAnOrdinaryUser()
    {
        var admin = await _factory.AdminAsync();
        var request = NewUser();

        var created = await admin.PostAsync<AdminUserDto>(ApiRoutes.AdminUsers, request, HttpStatusCode.Created);

        Assert.Equal(request.UserName, created.UserName);
        Assert.Equal(request.Email, created.Email);
        Assert.Equal(request.DisplayName, created.DisplayName);
        Assert.Equal(new[] { Roles.User }, created.Roles);
        Assert.False(created.LockedOut);
        Assert.False(created.TwoFactorEnabled);

        var users = await admin.GetAsync<List<AdminUserDto>>(ApiRoutes.AdminUsers);
        Assert.Contains(users, user => user.Id == created.Id);
    }

    [Fact]
    public async Task CreatedUser_WithoutADisplayName_IsShownByUserName()
    {
        var admin = await _factory.AdminAsync();
        var request = NewUser();
        request.DisplayName = "";

        var created = await admin.PostAsync<AdminUserDto>(ApiRoutes.AdminUsers, request, HttpStatusCode.Created);

        Assert.Equal(request.UserName, created.DisplayName);
    }

    [Fact]
    public async Task CreatedUser_SignsInAndReplacesTheAssignedPassword()
    {
        var user = await _factory.CreatePendingUserAsync();

        var login = await _factory.LoginAsync(user);
        Assert.True(login.User!.MustChangePassword);

        var changed = await user.PostAsync<LoginResponse>(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = TaskHubApiFactory.UserPassword });
        Assert.False(changed.User!.MustChangePassword);

        user.Password = TaskHubApiFactory.UserPassword;
        var secondLogin = await _factory.LoginAsync(user);

        Assert.False(secondLogin.User!.MustChangePassword);
        Assert.Equal(user.UserId, secondLogin.User.Id);
    }

    [Fact]
    public async Task UserName_IsNotCaseSensitiveAtSignIn()
    {
        var user = await _factory.CreateUserAsync();

        var login = await _factory.Anonymous().PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName.ToUpperInvariant(), Password = user.Password });

        Assert.Equal(user.UserId, login.User!.Id);
    }

    [Fact]
    public async Task CreatingAUser_WithATakenNameOrEmail_IsRefused()
    {
        var admin = await _factory.AdminAsync();
        var existing = await _factory.CreatePendingUserAsync();

        var sameName = NewUser();
        sameName.UserName = existing.UserName;

        var sameNameOtherCase = NewUser();
        sameNameOtherCase.UserName = existing.UserName.ToUpperInvariant();

        var sameEmail = NewUser();
        sameEmail.Email = existing.Email;

        foreach (var request in new[] { sameName, sameNameOtherCase, sameEmail })
        {
            var response = await admin.PostAsync(ApiRoutes.AdminUsers, request);
            await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
        }
    }

    [Theory]
    [InlineData("ab", "valid@example.test", "Assigned-Passw0rd-1")]
    [InlineData("validname", "not-an-email", "Assigned-Passw0rd-1")]
    [InlineData("validname", "valid@example.test", "weak")]
    [InlineData("validname", "valid@example.test", "")]
    [InlineData("name with spaces", "valid@example.test", "Assigned-Passw0rd-1")]
    public async Task CreatingAUser_WithInvalidData_IsRefused(string userName, string email, string password)
    {
        var admin = await _factory.AdminAsync();

        var response = await admin.PostAsync(ApiRoutes.AdminUsers, new CreateUserRequest { UserName = userName, Email = email, Password = password });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatingAUser_IgnoresRolesSentWithTheRequest()
    {
        // New accounts start as ordinary users; the Admin role is granted in a separate, deliberate step.
        var admin = await _factory.AdminAsync();
        var request = NewUser();

        var response = await admin.PostAsync(ApiRoutes.AdminUsers, new
        {
            userName = request.UserName,
            email = request.Email,
            password = request.Password,
            roles = new[] { Roles.Admin },
            mustChangePassword = false
        });

        var created = await response.ReadAsync<AdminUserDto>(HttpStatusCode.Created);
        Assert.Equal(new[] { Roles.User }, created.Roles);

        var user = await _factory.LoginAsync(request.UserName, request.Password);
        var me = await user.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.True(me.MustChangePassword);
    }

    [Fact]
    public async Task GrantingTheAdminRole_OpensTheAdminPanelAfterSigningInAgain()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        var updated = await admin.PutAsync<AdminUserDto>(ApiRoutes.AdminUserRoles(user.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.Admin, Roles.User } });
        Assert.Contains(Roles.Admin, updated.Roles);

        await _factory.LoginAsync(user);

        var response = await user.GetAsync(ApiRoutes.AdminUsers);
        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResettingAPassword_LetsTheUserInWithTheNewOne()
    {
        const string resetPassword = "Reset-Passw0rd-1";

        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        var reset = await admin.PutAsync(ApiRoutes.AdminUserPassword(user.UserId), new ResetUserPasswordRequest { NewPassword = resetPassword });
        await reset.ShouldBeAsync(HttpStatusCode.OK);

        user.Password = resetPassword;
        var login = await _factory.LoginAsync(user);

        Assert.NotNull(login.AccessToken);
        Assert.True(login.User!.MustChangePassword);
    }

    [Fact]
    public async Task ResettingAPassword_ToAWeakOne_IsRefused()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        var response = await admin.PutAsync(ApiRoutes.AdminUserPassword(user.UserId), new ResetUserPasswordRequest { NewPassword = "weak" });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);

        // The user's password and session are unchanged.
        var me = await user.GetAsync(ApiRoutes.Me);
        await me.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletedUser_DisappearsFromTheListAndTheDirectory()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("removed");

        var delete = await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var users = await admin.GetAsync<List<AdminUserDto>>(ApiRoutes.AdminUsers);
        var directory = await admin.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={user.UserName}");
        var deleteAgain = await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));

        Assert.DoesNotContain(users, existing => existing.Id == user.UserId);
        Assert.Empty(directory);
        await deleteAgain.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletedUser_FreesTheUserNameForANewAccount()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("reused");
        await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));

        var recreated = await admin.PostAsync<AdminUserDto>(ApiRoutes.AdminUsers, new CreateUserRequest
        {
            UserName = user.UserName,
            Email = user.Email,
            Password = TaskHubApiFactory.AssignedPassword
        }, HttpStatusCode.Created);

        // A new account, not the old one brought back: the old token and password do not fit it.
        Assert.NotEqual(user.UserId, recreated.Id);

        var withOldToken = await user.GetAsync(ApiRoutes.Me);
        var withOldPassword = await _factory.Anonymous().PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        await withOldToken.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await withOldPassword.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserDirectory_FindsUsersByNameForAnySignedInUser()
    {
        var user = await _factory.CreateUserAsync("searcher");
        var target = await _factory.CreateUserAsync("findme");

        var byUserName = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={target.UserName}");
        var byPartOfName = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={target.UserName[..8].ToUpperInvariant()}");
        var noMatch = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={Guid.NewGuid():N}");

        Assert.Equal(target.UserId, Assert.Single(byUserName).Id);
        Assert.Contains(byPartOfName, found => found.Id == target.UserId);
        Assert.Empty(noMatch);
    }

    [Fact]
    public async Task UserGroup_IsCreatedFilledAndDeleted()
    {
        var admin = await _factory.AdminAsync();
        var first = await _factory.CreateUserAsync("grouped");
        var second = await _factory.CreateUserAsync("grouped");

        var group = await admin.PostAsync<UserGroupDto>(ApiRoutes.UserGroups, new CreateUserGroupRequest { Name = $"Team {Guid.NewGuid():N}", Description = "A team" });
        await admin.PostAsync<UserGroupMemberDto>(ApiRoutes.UserGroupMembers(group.Id), new AddUserGroupMemberRequest { UserId = first.UserId });
        await admin.PostAsync<UserGroupMemberDto>(ApiRoutes.UserGroupMembers(group.Id), new AddUserGroupMemberRequest { UserId = second.UserId });

        var duplicate = await admin.PostAsync(ApiRoutes.UserGroupMembers(group.Id), new AddUserGroupMemberRequest { UserId = first.UserId });
        await duplicate.ReadErrorsAsync(HttpStatusCode.Conflict);

        var members = await admin.GetAsync<List<UserGroupMemberDto>>(ApiRoutes.UserGroupMembers(group.Id));
        var groups = await admin.GetAsync<List<UserGroupDto>>(ApiRoutes.UserGroups);

        Assert.Equal(2, members.Count);
        Assert.Equal(2, groups.Single(existing => existing.Id == group.Id).MemberCount);

        var remove = await admin.DeleteAsync(ApiRoutes.UserGroupMember(group.Id, first.UserId));
        var removeAgain = await admin.DeleteAsync(ApiRoutes.UserGroupMember(group.Id, first.UserId));
        await remove.ShouldBeAsync(HttpStatusCode.NoContent);
        await removeAgain.ShouldBeAsync(HttpStatusCode.NotFound);

        var delete = await admin.DeleteAsync(ApiRoutes.UserGroup(group.Id));
        var deleteAgain = await admin.DeleteAsync(ApiRoutes.UserGroup(group.Id));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);
        await deleteAgain.ShouldBeAsync(HttpStatusCode.NotFound);

        // Deleting the group removed the memberships, not the users.
        var stillThere = await second.GetAsync(ApiRoutes.Me);
        await stillThere.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletedUser_LeavesTheirUserGroups()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("grouped");
        var group = await admin.PostAsync<UserGroupDto>(ApiRoutes.UserGroups, new CreateUserGroupRequest { Name = $"Team {Guid.NewGuid():N}" });
        await admin.PostAsync<UserGroupMemberDto>(ApiRoutes.UserGroupMembers(group.Id), new AddUserGroupMemberRequest { UserId = user.UserId });

        var delete = await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var members = await admin.GetAsync<List<UserGroupMemberDto>>(ApiRoutes.UserGroupMembers(group.Id));
        Assert.Empty(members);
    }

    private static CreateUserRequest NewUser()
    {
        var userName = $"managed{Guid.NewGuid():N}"[..20];

        return new CreateUserRequest
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            DisplayName = "Managed user",
            Password = TaskHubApiFactory.AssignedPassword
        };
    }
}
