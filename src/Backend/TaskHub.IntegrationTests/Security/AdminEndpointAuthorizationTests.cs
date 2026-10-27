using System.Net;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Security;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// The administration endpoints are closed to an ordinary signed-in user, and a refused call changes nothing.
/// </summary>
public class AdminEndpointAuthorizationTests : IClassFixture<TaskHubApiFactory>
{
    private const string Id = "11111111-2222-3333-4444-555555555555";

    private readonly TaskHubApiFactory _factory;

    public AdminEndpointAuthorizationTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/v1/admin/users" },
        { "POST", "/api/v1/admin/users" },
        { "PUT", $"/api/v1/admin/users/{Id}/password" },
        { "PUT", $"/api/v1/admin/users/{Id}/roles" },
        { "PUT", $"/api/v1/admin/users/{Id}/lockout" },
        { "DELETE", $"/api/v1/admin/users/{Id}" },
        { "GET", "/api/v1/admin/logs/logins" },
        { "GET", "/api/v1/admin/logs/activity" },
        { "GET", "/api/v1/admin/security" },
        { "PUT", "/api/v1/admin/security" }
    };

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_IsForbiddenForAnOrdinaryUser(string method, string url)
    {
        var cast = await _factory.GetCastAsync();
        var hasBody = method is "POST" or "PUT";

        var response = await cast.Stranger.SendAsync(new HttpMethod(method), url, hasBody ? new { } : null);

        await response.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_IsForbiddenForAProjectOwner(string method, string url)
    {
        // Owning projects gives no rights outside them.
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var hasBody = method is "POST" or "PUT";

        var response = await scenario.Cast.Owner.SendAsync(new HttpMethod(method), url, hasBody ? new { } : null);

        await response.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/v1/ADMIN/users")]
    [InlineData("/api/v1/Admin/Users")]
    [InlineData("/api/v1/admin/users/")]
    [InlineData("/api/v1.0/admin/users")]
    [InlineData("/api/v1/admin//users")]
    [InlineData("/api/v1/admin/users?role=Admin")]
    [InlineData("/api/v1/admin/users;admin=true")]
    [InlineData("/api/v1/admin/%75sers")]
    public async Task AdminEndpoint_CannotBeReachedThroughAnotherSpellingOfItsAddress(string url)
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.GetAsync(url);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
    }

    [Theory]
    [InlineData("X-Forwarded-For", "127.0.0.1")]
    [InlineData("X-Original-URL", "/api/v1/auth/me")]
    [InlineData("X-Rewrite-URL", "/api/v1/auth/me")]
    [InlineData("X-HTTP-Method-Override", "GET")]
    [InlineData("X-User-Role", "Admin")]
    [InlineData("X-Roles", "Admin")]
    public async Task AdminEndpoint_IsNotOpenedByARequestHeader(string header, string value)
    {
        var user = await _factory.CreateUserAsync();
        user.Client.DefaultRequestHeaders.Add(header, value);

        var response = await user.GetAsync(ApiRoutes.AdminUsers);

        await response.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_CannotGrantThemselvesTheAdminRole()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.PutAsync(ApiRoutes.AdminUserRoles(user.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.Admin } });

        await response.ShouldBeAsync(HttpStatusCode.Forbidden);

        var me = await user.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.Equal(new[] { Roles.User }, me.Roles);

        // A new sign-in does not carry the role either.
        var login = await _factory.LoginAsync(user);
        Assert.Equal(new[] { Roles.User }, login.User!.Roles);
    }

    [Fact]
    public async Task User_CannotCreateAnAccount()
    {
        var user = await _factory.CreateUserAsync();
        var userName = $"intruder{Guid.NewGuid():N}"[..20];

        var response = await user.PostAsync(ApiRoutes.AdminUsers, new CreateUserRequest
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            Password = TaskHubApiFactory.AssignedPassword
        });

        await response.ShouldBeAsync(HttpStatusCode.Forbidden);

        var admin = await _factory.AdminAsync();
        var users = await admin.GetAsync<List<AdminUserDto>>(ApiRoutes.AdminUsers);
        Assert.DoesNotContain(users, existing => existing.UserName == userName);
    }

    [Fact]
    public async Task User_CannotDeleteLockOrResetAnotherAccount()
    {
        var attacker = await _factory.CreateUserAsync("attacker");
        var victim = await _factory.CreateUserAsync("victim");

        var delete = await attacker.DeleteAsync(ApiRoutes.AdminUser(victim.UserId));
        var lockout = await attacker.PutAsync(ApiRoutes.AdminUserLockout(victim.UserId), new SetUserLockoutRequest { LockedOut = true });
        var reset = await attacker.PutAsync(ApiRoutes.AdminUserPassword(victim.UserId), new ResetUserPasswordRequest { NewPassword = "Attacker-Passw0rd-1" });
        var roles = await attacker.PutAsync(ApiRoutes.AdminUserRoles(victim.UserId), new UpdateUserRolesRequest { Roles = Array.Empty<string>() });

        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);
        await lockout.ShouldBeAsync(HttpStatusCode.Forbidden);
        await reset.ShouldBeAsync(HttpStatusCode.Forbidden);
        await roles.ShouldBeAsync(HttpStatusCode.Forbidden);

        // The victim's session and password are exactly as they were.
        var me = await victim.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.Equal(new[] { Roles.User }, me.Roles);

        var login = await _factory.LoginAsync(victim);
        Assert.NotNull(login.AccessToken);
    }

    [Fact]
    public async Task User_CannotDeleteOrLockTheAdministrator()
    {
        var attacker = await _factory.CreateUserAsync("attacker");
        var admin = await _factory.AdminAsync();

        var delete = await attacker.DeleteAsync(ApiRoutes.AdminUser(admin.UserId));
        var lockout = await attacker.PutAsync(ApiRoutes.AdminUserLockout(admin.UserId), new SetUserLockoutRequest { LockedOut = true });
        var reset = await attacker.PutAsync(ApiRoutes.AdminUserPassword(admin.UserId), new ResetUserPasswordRequest { NewPassword = "Attacker-Passw0rd-1" });

        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);
        await lockout.ShouldBeAsync(HttpStatusCode.Forbidden);
        await reset.ShouldBeAsync(HttpStatusCode.Forbidden);

        var stillWorks = await admin.GetAsync(ApiRoutes.AdminUsers);
        await stillWorks.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_CannotChangeTheSecuritySettings()
    {
        var user = await _factory.CreateUserAsync();
        var admin = await _factory.AdminAsync();
        var before = await admin.GetAsync<SecuritySettingsDto>(ApiRoutes.AdminSecurity);

        var response = await user.PutAsync(ApiRoutes.AdminSecurity, new SecuritySettingsDto
        {
            LockoutEnabled = false,
            MaxFailedAccessAttempts = 100,
            LockoutMinutes = 1,
            RegistrationEnabled = true,
            PasswordPolicy = new PasswordPolicyDto { MinLength = 4, RequireDigit = false, RequireLowercase = false, RequireUppercase = false }
        });

        await response.ShouldBeAsync(HttpStatusCode.Forbidden);

        var after = await admin.GetAsync<SecuritySettingsDto>(ApiRoutes.AdminSecurity);
        Assert.Equal(before.LockoutEnabled, after.LockoutEnabled);
        Assert.Equal(before.MaxFailedAccessAttempts, after.MaxFailedAccessAttempts);
        Assert.Equal(before.RegistrationEnabled, after.RegistrationEnabled);
        Assert.Equal(before.PasswordPolicy.MinLength, after.PasswordPolicy.MinLength);
    }

    [Fact]
    public async Task User_CannotManageAnotherOwnersTeam()
    {
        var user = await _factory.CreateUserAsync();
        var admin = await _factory.AdminAsync();
        var group = await admin.PostAsync<UserGroupDto>(ApiRoutes.UserGroups, new CreateUserGroupRequest { Name = $"Group {Guid.NewGuid():N}" });

        var join = await user.PostAsync(ApiRoutes.UserGroupMembers(group.Id), new AddUserGroupMemberRequest { UserId = user.UserId });
        var delete = await user.DeleteAsync(ApiRoutes.UserGroup(group.Id));

        await join.ShouldBeAsync(HttpStatusCode.Forbidden);
        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);

        var groups = await admin.GetAsync<List<UserGroupDto>>(ApiRoutes.UserGroups);
        Assert.Equal(0, groups.Single(existing => existing.Id == group.Id).MemberCount);
    }

    [Fact]
    public async Task SecondAdministrator_HasFullAdminAccess()
    {
        var secondAdmin = await _factory.CreateAdminAsync();

        var users = await secondAdmin.GetAsync(ApiRoutes.AdminUsers);
        var security = await secondAdmin.GetAsync(ApiRoutes.AdminSecurity);
        var logs = await secondAdmin.GetAsync(ApiRoutes.AdminActivityLogs);

        await users.ShouldBeAsync(HttpStatusCode.OK);
        await security.ShouldBeAsync(HttpStatusCode.OK);
        await logs.ShouldBeAsync(HttpStatusCode.OK);
    }
}
