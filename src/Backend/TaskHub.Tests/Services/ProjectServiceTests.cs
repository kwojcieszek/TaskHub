using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class ProjectServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IProjectService _projects;

    public ProjectServiceTests()
    {
        _projects = _host.Get<IProjectService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Create_MakesTheCallerOwnerAndTheProjectActive()
    {
        var owner = await _host.CreateUserAsync("owner");

        var project = await _host.CreateProjectAsync(owner);
        var members = await _projects.GetMembersAsync(project.Id);

        Assert.True(project.IsActive);
        Assert.Equal(owner.Id, project.OwnerId);
        var member = Assert.Single(members);
        Assert.Equal(owner.Id, member.UserId);
        Assert.Equal(ProjectMemberRole.Owner.ToString(), member.Role);
    }

    [Fact]
    public async Task GetAccessibleProjects_ReturnsOnlyProjectsOfTheCaller()
    {
        var owner = await _host.CreateUserAsync("owner");
        var stranger = await _host.CreateUserAsync("stranger");
        await _host.CreateProjectAsync(owner, "mine");
        await _host.CreateProjectAsync(stranger, "theirs");

        _host.SignIn(owner);
        var projects = await _projects.GetAccessibleProjectsAsync();

        Assert.Equal("mine", Assert.Single(projects).Name);
    }

    [Fact]
    public async Task GetAccessibleProjects_ReturnsEverythingForAnAdministrator()
    {
        var owner = await _host.CreateUserAsync("owner");
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        await _host.CreateProjectAsync(owner, "first");
        await _host.CreateProjectAsync(owner, "second");

        _host.SignIn(admin, Roles.Admin);
        var projects = await _projects.GetAccessibleProjectsAsync();

        Assert.Equal(2, projects.Count);
    }

    [Fact]
    public async Task GetById_IsForbiddenForSomeoneOutsideTheProject()
    {
        var owner = await _host.CreateUserAsync("owner");
        var stranger = await _host.CreateUserAsync("stranger");
        var project = await _host.CreateProjectAsync(owner);

        _host.SignIn(stranger);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.GetByIdAsync(project.Id));
    }

    [Fact]
    public async Task Viewer_CanReadButCannotChangeTasks()
    {
        var owner = await _host.CreateUserAsync("owner");
        var viewer = await _host.CreateUserAsync("viewer");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, viewer, ProjectMemberRole.Viewer);
        var tasks = _host.Get<ITaskItemService>();

        _host.SignIn(viewer);
        var visible = await tasks.GetByProjectAsync(project.Id);

        Assert.Empty(visible);
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "task" }));
    }

    [Fact]
    public async Task Editor_CanRenameButCannotDeactivateOrManageMembers()
    {
        var owner = await _host.CreateUserAsync("owner");
        var editor = await _host.CreateUserAsync("editor");
        var other = await _host.CreateUserAsync("other");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, editor, ProjectMemberRole.Editor);

        _host.SignIn(editor);
        var renamed = await _projects.UpdateAsync(project.Id, new UpdateProjectRequest { Name = "renamed" });

        Assert.Equal("renamed", renamed.Name);
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _projects.UpdateAsync(project.Id, new UpdateProjectRequest { Name = "renamed", IsActive = false }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _projects.AddMemberAsync(project.Id, new AddProjectMemberRequest { UserId = other.Id, Role = ProjectMemberRole.Viewer }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.DeleteAsync(project.Id));
    }

    [Fact]
    public async Task Owner_CanDeactivateAndReactivateTheProject()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner);

        var deactivated = await _projects.UpdateAsync(project.Id, new UpdateProjectRequest { Name = project.Name, IsActive = false });
        var reactivated = await _projects.UpdateAsync(project.Id, new UpdateProjectRequest { Name = project.Name });

        Assert.False(deactivated.IsActive);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task AddMember_RejectsSomeoneWhoIsAlreadyAMember()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, member, ProjectMemberRole.Viewer);

        await Assert.ThrowsAsync<ConflictException>(
            () => _projects.AddMemberAsync(project.Id, new AddProjectMemberRequest { UserId = member.Id, Role = ProjectMemberRole.Editor }));
    }

    [Fact]
    public async Task RemoveMember_RefusesToRemoveTheOwner()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner);

        await Assert.ThrowsAsync<ConflictException>(() => _projects.RemoveMemberAsync(project.Id, owner.Id));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.test/repo.git")]
    [InlineData("not a url")]
    public async Task Create_RejectsGitUrlThatIsNotHttp(string gitUrl)
    {
        var owner = await _host.CreateUserAsync("owner");
        _host.SignIn(owner);

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _projects.CreateAsync(new CreateProjectRequest { Name = "project", GitUrl = gitUrl }));
    }

    [Theory]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz4=")]
    [InlineData("fa-home")]
    [InlineData("pi-home\" onclick=\"x")]
    public async Task Create_RejectsIconThatIsNeitherAKnownNameNorARasterImage(string icon)
    {
        var owner = await _host.CreateUserAsync("owner");
        _host.SignIn(owner);

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _projects.CreateAsync(new CreateProjectRequest { Name = "project", Icon = icon }));
    }

    [Fact]
    public async Task Create_AcceptsIconNameRasterImageAndHttpsGitUrl()
    {
        var owner = await _host.CreateUserAsync("owner");
        _host.SignIn(owner);

        var named = await _projects.CreateAsync(new CreateProjectRequest { Name = "named", Icon = "pi-briefcase", GitUrl = "https://example.test/repo.git" });
        var image = await _projects.CreateAsync(new CreateProjectRequest { Name = "image", Icon = "data:image/png;base64,iVBORw0KGgo=" });

        Assert.Equal("pi-briefcase", named.Icon);
        Assert.Equal("https://example.test/repo.git", named.GitUrl);
        Assert.StartsWith("data:image/png;base64,", image.Icon);
    }
}
