using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Interfaces;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class AdminUserServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IAdminUserService _admin;

    public AdminUserServiceTests()
    {
        _admin = _host.Get<IAdminUserService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task CreateUser_AddsAnOrdinaryUserWhoMustChangeTheAssignedPassword()
    {
        var created = await _admin.CreateUserAsync(new CreateUserRequest
        {
            UserName = "anna",
            Email = "anna@example.test",
            Password = "Assigned1"
        });

        var user = await _host.Users.FindByIdAsync(created.Id.ToString());

        Assert.Equal(new[] { Roles.User }, created.Roles);
        Assert.Equal("anna", created.DisplayName);
        Assert.True(user!.MustChangePassword);
        Assert.True(await _host.Users.CheckPasswordAsync(user, "Assigned1"));
    }

    [Fact]
    public async Task CreateUser_RejectsATakenNameAndATooShortPassword()
    {
        await _host.CreateUserAsync("anna");

        await Assert.ThrowsAsync<ValidationFailedException>(() => _admin.CreateUserAsync(new CreateUserRequest
        {
            UserName = "anna",
            Email = "other@example.test",
            Password = "Assigned1"
        }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _admin.CreateUserAsync(new CreateUserRequest
        {
            UserName = "jan",
            Email = "jan@example.test",
            Password = "abc"
        }));
    }

    [Fact]
    public async Task ResetPassword_SetsThePasswordEndsSessionsAndForcesAChange()
    {
        var user = await _host.CreateUserAsync("anna");

        await _admin.ResetPasswordAsync(user.Id, new ResetUserPasswordRequest { NewPassword = "Reset1234" });

        Assert.True(user.MustChangePassword);
        Assert.Equal(1, user.TokenVersion);
        Assert.True(await _host.Users.CheckPasswordAsync(user, "Reset1234"));
        Assert.False(await _host.Users.CheckPasswordAsync(user, TestHost.DefaultPassword));
    }

    [Fact]
    public async Task UpdateRoles_ReplacesRolesAndEndsTheUsersSessions()
    {
        var user = await _host.CreateUserAsync("anna", Roles.User);

        var updated = await _admin.UpdateRolesAsync(user.Id, new UpdateUserRolesRequest { Roles = new[] { Roles.Admin } });

        Assert.Equal(new[] { Roles.Admin }, updated.Roles);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task UpdateRoles_RejectsARoleThatDoesNotExist()
    {
        var user = await _host.CreateUserAsync("anna", Roles.User);

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _admin.UpdateRolesAsync(user.Id, new UpdateUserRolesRequest { Roles = new[] { "Superuser" } }));
        Assert.Equal(new[] { Roles.User }, await _host.Users.GetRolesAsync(user));
    }

    [Fact]
    public async Task SetLockout_LocksAndUnlocksTheUser()
    {
        var user = await _host.CreateUserAsync("anna");

        var locked = await _admin.SetLockoutAsync(user.Id, new SetUserLockoutRequest { LockedOut = true });
        var unlocked = await _admin.SetLockoutAsync(user.Id, new SetUserLockoutRequest { LockedOut = false });

        Assert.True(locked.LockedOut);
        Assert.False(unlocked.LockedOut);
    }

    [Fact]
    public async Task DeleteUser_RemovesTheAccount()
    {
        var user = await _host.CreateUserAsync("anna");

        await _admin.DeleteUserAsync(user.Id);

        Assert.Null(await _host.Users.FindByNameAsync("anna"));
        await Assert.ThrowsAsync<NotFoundException>(() => _admin.DeleteUserAsync(user.Id));
    }
}
