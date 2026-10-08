using TaskHub.Application.Common;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class ProjectAccessGuardTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IProjectAccessGuard _guard;

    public ProjectAccessGuardTests()
    {
        _guard = _host.Get<IProjectAccessGuard>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task EachRole_IsAllowedExactlyWhatItStandsFor()
    {
        var owner = await _host.CreateUserAsync("owner");
        var editor = await _host.CreateUserAsync("editor");
        var viewer = await _host.CreateUserAsync("viewer");
        var outsider = await _host.CreateUserAsync("outsider");
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, editor, ProjectMemberRole.Editor);
        await _host.AddMemberAsync(project, owner, viewer, ProjectMemberRole.Viewer);

        _host.SignIn(owner);
        await _guard.EnsureCanViewAsync(project.Id);
        await _guard.EnsureCanEditAsync(project.Id);
        await _guard.EnsureCanManageAsync(project.Id);
        Assert.True(await _guard.CanManageAsync(project.Id));

        _host.SignIn(editor);
        await _guard.EnsureCanViewAsync(project.Id);
        await _guard.EnsureCanEditAsync(project.Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanManageAsync(project.Id));
        Assert.False(await _guard.CanManageAsync(project.Id));

        _host.SignIn(viewer);
        await _guard.EnsureCanViewAsync(project.Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanEditAsync(project.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanManageAsync(project.Id));

        _host.SignIn(outsider);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanViewAsync(project.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanEditAsync(project.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanManageAsync(project.Id));
        Assert.False(await _guard.CanManageAsync(project.Id));

        _host.SignIn(admin, Roles.Admin);
        await _guard.EnsureCanViewAsync(project.Id);
        await _guard.EnsureCanEditAsync(project.Id);
        await _guard.EnsureCanManageAsync(project.Id);
        Assert.True(await _guard.CanManageAsync(project.Id));
    }

    [Fact]
    public async Task MissingProject_IsAnsweredLikeOneWithoutAccess()
    {
        var user = await _host.CreateUserAsync("user");
        _host.SignIn(user);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanViewAsync(Guid.NewGuid()));
        Assert.False(await _guard.CanManageAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MembershipOfOneProject_SaysNothingAboutAnother()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var shared = await _host.CreateProjectAsync(owner, "Shared");
        var secret = await _host.CreateProjectAsync(owner, "Secret");
        await _host.AddMemberAsync(shared, owner, member, ProjectMemberRole.Editor);

        _host.SignIn(member);

        await _guard.EnsureCanEditAsync(shared.Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanViewAsync(secret.Id));
    }

    [Fact]
    public async Task ProjectGroup_IsManagedByItsOwnerOrAnAdministrator()
    {
        var owner = await _host.CreateUserAsync("owner");
        var other = await _host.CreateUserAsync("other");
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        _host.SignIn(owner);
        var group = await _host.Get<IProjectGroupService>().CreateAsync(new CreateProjectGroupRequest { Name = "Group" });

        await _guard.EnsureCanManageGroupAsync(group.Id);

        _host.SignIn(other);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _guard.EnsureCanManageGroupAsync(group.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _guard.EnsureCanManageGroupAsync(Guid.NewGuid()));

        _host.SignIn(admin, Roles.Admin);
        await _guard.EnsureCanManageGroupAsync(group.Id);
    }
}
