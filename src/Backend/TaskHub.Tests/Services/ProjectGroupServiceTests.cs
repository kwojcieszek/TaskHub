using TaskHub.Application.Common;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class ProjectGroupServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IProjectGroupService _groups;
    private readonly IProjectService _projects;

    public ProjectGroupServiceTests()
    {
        _groups = _host.Get<IProjectGroupService>();
        _projects = _host.Get<IProjectService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Create_MakesTheCallerTheOwner()
    {
        var owner = await _host.CreateUserAsync("owner");
        _host.SignIn(owner);

        var group = await _groups.CreateAsync(new CreateProjectGroupRequest { Name = "Clients", Description = "External work" });

        Assert.Equal(owner.Id, group.OwnerId);
        Assert.Equal("Clients", group.Name);
        Assert.Equal(0, group.ProjectCount);
    }

    [Fact]
    public async Task Group_IsVisibleToItsOwnerAndToMembersOfItsProjects()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var outsider = await _host.CreateUserAsync("outsider");
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        _host.SignIn(owner);
        var group = await _groups.CreateAsync(new CreateProjectGroupRequest { Name = "Clients" });
        var project = await _projects.CreateAsync(new CreateProjectRequest { Name = "Shared", ProjectGroupId = group.Id });
        await _projects.AddMemberAsync(project.Id, new AddProjectMemberRequest { UserId = member.Id, Role = ProjectMemberRole.Viewer });

        _host.SignIn(member);
        var asMember = await _groups.GetAccessibleGroupsAsync();
        var memberProjects = await _projects.GetAccessibleProjectsAsync(group.Id);

        _host.SignIn(outsider);
        var asOutsider = await _groups.GetAccessibleGroupsAsync();

        _host.SignIn(admin, Roles.Admin);
        var asAdmin = await _groups.GetAccessibleGroupsAsync();

        Assert.Equal(group.Id, Assert.Single(asMember).Id);
        Assert.Equal(project.Id, Assert.Single(memberProjects).Id);
        Assert.Empty(asOutsider);
        Assert.Equal(group.Id, Assert.Single(asAdmin).Id);

        _host.SignIn(outsider);
        await Assert.ThrowsAsync<NotFoundException>(() => _groups.GetByIdAsync(group.Id));
    }

    [Fact]
    public async Task OnlyTheOwnerOrAnAdministrator_ChangesOrDeletesTheGroup()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        _host.SignIn(owner);
        var group = await _groups.CreateAsync(new CreateProjectGroupRequest { Name = "Clients" });
        var project = await _projects.CreateAsync(new CreateProjectRequest { Name = "Shared", ProjectGroupId = group.Id });
        await _projects.AddMemberAsync(project.Id, new AddProjectMemberRequest { UserId = member.Id, Role = ProjectMemberRole.Editor });

        _host.SignIn(member);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _groups.UpdateAsync(group.Id, new UpdateProjectGroupRequest { Name = "Hijacked" }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _groups.DeleteAsync(group.Id));

        _host.SignIn(owner);
        var renamed = await _groups.UpdateAsync(group.Id, new UpdateProjectGroupRequest { Name = "Customers" });

        _host.SignIn(admin, Roles.Admin);
        await _groups.DeleteAsync(group.Id);

        Assert.Equal("Customers", renamed.Name);
        Assert.NotNull(renamed.UpdatedAt);
        Assert.Empty(await _groups.GetAccessibleGroupsAsync());
    }

    [Fact]
    public async Task DeletingAGroup_KeepsItsProjects_WithoutAGroup()
    {
        var owner = await _host.CreateUserAsync("owner");
        _host.SignIn(owner);
        var group = await _groups.CreateAsync(new CreateProjectGroupRequest { Name = "Clients" });
        var project = await _projects.CreateAsync(new CreateProjectRequest { Name = "Kept", ProjectGroupId = group.Id });

        await _groups.DeleteAsync(group.Id);
        var kept = await _projects.GetByIdAsync(project.Id);

        Assert.Null(kept.ProjectGroupId);
        Assert.Equal("Kept", kept.Name);
    }

    [Fact]
    public async Task Project_CannotBePutIntoSomeoneElsesGroup()
    {
        var owner = await _host.CreateUserAsync("owner");
        var other = await _host.CreateUserAsync("other");
        _host.SignIn(owner);
        var group = await _groups.CreateAsync(new CreateProjectGroupRequest { Name = "Clients" });

        _host.SignIn(other);
        var ownProject = await _projects.CreateAsync(new CreateProjectRequest { Name = "Own" });

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.CreateAsync(new CreateProjectRequest { Name = "Planted", ProjectGroupId = group.Id }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.SetGroupAsync(ownProject.Id, new SetProjectGroupRequest { ProjectGroupId = group.Id }));
        await Assert.ThrowsAsync<NotFoundException>(() => _projects.SetGroupAsync(ownProject.Id, new SetProjectGroupRequest { ProjectGroupId = Guid.NewGuid() }));
    }
}
