using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

/// <summary>
/// Rules that keep an administrator from locking the system out of itself and a project from
/// pointing at people or roles that have no place in it.
/// </summary>
public class AccountSafetyRulesTests : IDisposable
{
    private readonly TestHost _host = new();

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Administrator_CannotDeleteLockOrDemoteTheirOwnAccount()
    {
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        var service = _host.Get<IAdminUserService>();
        _host.SignIn(admin, Roles.Admin);

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteUserAsync(admin.Id));
        await Assert.ThrowsAsync<ConflictException>(() => service.SetLockoutAsync(admin.Id, new SetUserLockoutRequest { LockedOut = true }));
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateRolesAsync(admin.Id, new UpdateUserRolesRequest { Roles = new[] { Roles.User } }));

        Assert.NotNull(await _host.Users.FindByIdAsync(admin.Id.ToString()));
        Assert.True(await _host.Users.IsInRoleAsync(admin, Roles.Admin));
        Assert.False(await _host.Users.IsLockedOutAsync(admin));
    }

    [Fact]
    public async Task Administrator_MayStillChangeOwnRolesAsLongAsTheAdminRoleStays_AndUnlockThemselves()
    {
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        var service = _host.Get<IAdminUserService>();
        _host.SignIn(admin, Roles.Admin);

        var updated = await service.UpdateRolesAsync(admin.Id, new UpdateUserRolesRequest { Roles = new[] { Roles.Admin, Roles.User } });
        var unlocked = await service.SetLockoutAsync(admin.Id, new SetUserLockoutRequest { LockedOut = false });

        Assert.Contains(Roles.User, updated.Roles);
        Assert.Contains(Roles.Admin, updated.Roles);
        Assert.False(unlocked.LockedOut);
    }

    [Fact]
    public async Task Administrator_MayDoAllThatToAnotherAdministrator()
    {
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        var second = await _host.CreateUserAsync("second", Roles.Admin);
        var third = await _host.CreateUserAsync("third", Roles.Admin);
        var service = _host.Get<IAdminUserService>();
        _host.SignIn(admin, Roles.Admin);

        var demoted = await service.UpdateRolesAsync(second.Id, new UpdateUserRolesRequest { Roles = new[] { Roles.User } });
        var locked = await service.SetLockoutAsync(second.Id, new SetUserLockoutRequest { LockedOut = true });
        await service.DeleteUserAsync(third.Id);

        Assert.DoesNotContain(Roles.Admin, demoted.Roles);
        Assert.True(locked.LockedOut);
        Assert.Null(await _host.Users.FindByIdAsync(third.Id.ToString()));
    }

    [Fact]
    public async Task Task_CanOnlyBeAssignedToSomeoneInTheProject()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var outsider = await _host.CreateUserAsync("outsider");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, member, ProjectMemberRole.Viewer);
        var tasks = _host.Get<ITaskItemService>();

        var toMember = await tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "For a member", AssignedUserId = member.Id });
        var unassigned = await tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "For nobody" });

        await Assert.ThrowsAsync<ValidationFailedException>(() => tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "For an outsider", AssignedUserId = outsider.Id }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "For a ghost", AssignedUserId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => tasks.UpdateAsync(project.Id, unassigned.Id, new UpdateTaskItemRequest { Title = unassigned.Title, AssignedUserId = outsider.Id }));
        Assert.Equal(member.Id, toMember.AssignedUserId);
        Assert.Null((await tasks.GetByIdAsync(project.Id, unassigned.Id)).AssignedUserId);
    }

    [Fact]
    public async Task Task_KeepsAnAssigneeWhoLeftTheProject_UntilTheAssigneeIsChanged()
    {
        // Only a change of the assignee is checked, so an old task can still be edited after its assignee left.
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, member, ProjectMemberRole.Editor);
        var tasks = _host.Get<ITaskItemService>();
        var task = await tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Assigned", AssignedUserId = member.Id });
        await _host.Get<IProjectService>().RemoveMemberAsync(project.Id, member.Id);

        var renamed = await tasks.UpdateAsync(project.Id, task.Id, new UpdateTaskItemRequest { Title = "Renamed", AssignedUserId = member.Id });
        var cleared = await tasks.UpdateAsync(project.Id, task.Id, new UpdateTaskItemRequest { Title = "Renamed", AssignedUserId = null });

        Assert.Equal("Renamed", renamed.Title);
        Assert.Null(cleared.AssignedUserId);
        await Assert.ThrowsAsync<ValidationFailedException>(() => tasks.UpdateAsync(project.Id, task.Id, new UpdateTaskItemRequest { Title = "Renamed", AssignedUserId = member.Id }));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task Member_CannotBeAddedWithARoleThatDoesNotExist(int role)
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var project = await _host.CreateProjectAsync(owner);
        var projects = _host.Get<IProjectService>();

        await Assert.ThrowsAsync<ValidationFailedException>(() => projects.AddMemberAsync(project.Id, new AddProjectMemberRequest { UserId = member.Id, Role = (ProjectMemberRole)role }));
        Assert.Single(await projects.GetMembersAsync(project.Id));
    }
}
