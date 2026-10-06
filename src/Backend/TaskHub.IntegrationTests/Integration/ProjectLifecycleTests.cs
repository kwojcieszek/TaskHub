using System.Net;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Integration;

/// <summary>
/// A project from creation to deletion, with its members and its place in a project group.
/// </summary>
public class ProjectLifecycleTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public ProjectLifecycleTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatedProject_BelongsToItsCreatorAndIsActive()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest
        {
            Name = "Website",
            Description = "The public website",
            Icon = "pi-globe",
            GitUrl = "https://example.com/team/website.git"
        });
        var project = await response.ReadAsync<ProjectDto>(HttpStatusCode.Created);

        Assert.Equal("Website", project.Name);
        Assert.Equal("The public website", project.Description);
        Assert.Equal("pi-globe", project.Icon);
        Assert.Equal("https://example.com/team/website.git", project.GitUrl);
        Assert.Equal(user.UserId, project.OwnerId);
        Assert.True(project.IsActive);
        Assert.Equal(0, project.TaskCount);
        Assert.EndsWith($"/projects/{project.Id}", response.Headers.Location?.AbsolutePath, StringComparison.OrdinalIgnoreCase);

        var members = await user.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(project.Id));
        var owner = Assert.Single(members);
        Assert.Equal(user.UserId, owner.UserId);
        Assert.Equal(ProjectMemberRole.Owner.ToString(), owner.Role);
    }

    [Fact]
    public async Task CreatedProject_IsListedAndCanBeOpened()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync("Listed");

        var list = await user.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        var opened = await user.GetAsync<ProjectDto>(ApiRoutes.Project(project.Id));

        Assert.Equal(project.Id, Assert.Single(list).Id);
        Assert.Equal("Listed", opened.Name);
    }

    [Fact]
    public async Task UpdatedProject_KeepsTheNewValues()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync("Before");

        var updated = await user.PutAsync<ProjectDto>(ApiRoutes.Project(project.Id), new UpdateProjectRequest
        {
            Name = "After",
            Description = "Changed",
            Icon = "pi-star",
            GitUrl = "http://example.com/repo",
            IsActive = true
        });

        Assert.Equal("After", updated.Name);
        Assert.Equal("Changed", updated.Description);
        Assert.Equal("pi-star", updated.Icon);
        Assert.NotNull(updated.UpdatedAt);

        var reloaded = await user.GetAsync<ProjectDto>(ApiRoutes.Project(project.Id));
        Assert.Equal("After", reloaded.Name);
    }

    [Fact]
    public async Task DeactivatedProject_HidesItsTasksFromTheOverviewButKeepsThem()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();
        var task = await user.CreateTaskAsync(project.Id, "Hidden", DateTime.UtcNow.AddDays(1));

        var deactivated = await user.PutAsync<ProjectDto>(ApiRoutes.Project(project.Id), new UpdateProjectRequest { Name = project.Name, IsActive = false });
        Assert.False(deactivated.IsActive);

        var overview = await user.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);
        var calendar = await user.GetAsync<List<TaskItemDto>>($"{ApiRoutes.AllTasks}?from=2000-01-01&to=2100-01-01");
        var inProject = await user.GetAsync<List<TaskItemDto>>(ApiRoutes.Tasks(project.Id));

        Assert.DoesNotContain(overview, existing => existing.Id == task.Id);
        Assert.DoesNotContain(calendar, existing => existing.Id == task.Id);
        Assert.Equal(task.Id, Assert.Single(inProject).Id);

        var reactivated = await user.PutAsync<ProjectDto>(ApiRoutes.Project(project.Id), new UpdateProjectRequest { Name = project.Name, IsActive = true });
        Assert.True(reactivated.IsActive);

        overview = await user.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);
        Assert.Contains(overview, existing => existing.Id == task.Id);
    }

    [Fact]
    public async Task DeletedProject_IsGoneWithEverythingInIt()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var owner = scenario.Cast.Owner;
        var admin = scenario.Cast.Admin;

        var delete = await owner.DeleteAsync(ApiRoutes.Project(scenario.Project.Id));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var list = await owner.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        var overview = await owner.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);
        Assert.DoesNotContain(list, project => project.Id == scenario.Project.Id);
        Assert.DoesNotContain(overview, task => task.ProjectId == scenario.Project.Id);

        // The administrator may look anywhere, so for them the remains would show up as something other than "not found".
        var project = await admin.GetAsync(ApiRoutes.Project(scenario.Project.Id));
        var task = await admin.GetAsync(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id));
        var subTask = await admin.GetAsync(ApiRoutes.Task(scenario.Project.Id, scenario.SubTask.Id));
        var attachment = await admin.GetAsync(ApiRoutes.AttachmentContent(scenario.Project.Id, scenario.LeafTask.Id, scenario.Attachment.Id));
        var deleteAgain = await admin.DeleteAsync(ApiRoutes.Project(scenario.Project.Id));

        await project.ShouldBeAsync(HttpStatusCode.NotFound);
        await task.ShouldBeAsync(HttpStatusCode.NotFound);
        await subTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await attachment.ShouldBeAsync(HttpStatusCode.NotFound);
        await deleteAgain.ShouldBeAsync(HttpStatusCode.NotFound);

        // The stored file went with the last attachment that referred to it.
        var stored = Directory.Exists(_factory.AttachmentsPath)
            ? Directory.GetFiles(_factory.AttachmentsPath, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        Assert.DoesNotContain(stored, path => Path.GetFileName(path).StartsWith(scenario.Attachment.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Members_AreAddedListedAndRemoved()
    {
        var owner = await _factory.CreateUserAsync("owner");
        var editor = await _factory.CreateUserAsync("editor");
        var viewer = await _factory.CreateUserAsync("viewer");
        var project = await owner.CreateProjectAsync();

        var addedEditor = await owner.AddMemberAsync(project.Id, editor.UserId, ProjectMemberRole.Editor);
        var addedViewer = await owner.AddMemberAsync(project.Id, viewer.UserId, ProjectMemberRole.Viewer);

        Assert.Equal(editor.UserName, addedEditor.UserName);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), addedEditor.Role);
        Assert.Equal(ProjectMemberRole.Viewer.ToString(), addedViewer.Role);

        var duplicate = await owner.PostAsync(ApiRoutes.ProjectMembers(project.Id), new AddProjectMemberRequest { UserId = editor.UserId, Role = ProjectMemberRole.Viewer });
        await duplicate.ReadErrorsAsync(HttpStatusCode.Conflict);

        var members = await viewer.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(project.Id));
        Assert.Equal(3, members.Count);

        // The project now shows up for the new members.
        var editorProjects = await editor.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        Assert.Equal(project.Id, Assert.Single(editorProjects).Id);

        var remove = await owner.DeleteAsync(ApiRoutes.ProjectMember(project.Id, editor.UserId));
        var removeAgain = await owner.DeleteAsync(ApiRoutes.ProjectMember(project.Id, editor.UserId));
        await remove.ShouldBeAsync(HttpStatusCode.NoContent);
        await removeAgain.ShouldBeAsync(HttpStatusCode.NotFound);

        editorProjects = await editor.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        Assert.Empty(editorProjects);
    }

    [Fact]
    public async Task Member_AddedWithoutARole_IsAViewer()
    {
        var owner = await _factory.CreateUserAsync("owner");
        var member = await _factory.CreateUserAsync("member");
        var project = await owner.CreateProjectAsync();

        var added = await owner.PostAsync<ProjectMemberDto>(ApiRoutes.ProjectMembers(project.Id), new { userId = member.UserId });

        Assert.Equal(ProjectMemberRole.Viewer.ToString(), added.Role);

        var edit = await member.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "Not allowed" });
        await edit.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProjectGroup_CollectsProjectsAndLetsThemGoWhenDeleted()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.PostAsync(ApiRoutes.ProjectGroups, new CreateProjectGroupRequest { Name = "Clients", Description = "Client work" });
        var group = await response.ReadAsync<ProjectGroupDto>(HttpStatusCode.Created);
        Assert.Equal(user.UserId, group.OwnerId);
        Assert.Equal(0, group.ProjectCount);

        var inGroup = await user.CreateProjectAsync("In the group", group.Id);
        var outside = await user.CreateProjectAsync("Outside");
        Assert.Equal(group.Id, inGroup.ProjectGroupId);
        Assert.Equal("Clients", inGroup.ProjectGroupName);

        var moved = await user.PutAsync<ProjectDto>(ApiRoutes.ProjectGroupOf(outside.Id), new SetProjectGroupRequest { ProjectGroupId = group.Id });
        Assert.Equal(group.Id, moved.ProjectGroupId);

        var groupProjects = await user.GetAsync<List<ProjectDto>>(ApiRoutes.ProjectGroupProjects(group.Id));
        var filtered = await user.GetAsync<List<ProjectDto>>($"{ApiRoutes.Projects}?groupId={group.Id}");
        var reloadedGroup = await user.GetAsync<ProjectGroupDto>(ApiRoutes.ProjectGroup(group.Id));
        Assert.Equal(2, groupProjects.Count);
        Assert.Equal(2, filtered.Count);
        Assert.Equal(2, reloadedGroup.ProjectCount);

        var renamed = await user.PutAsync<ProjectGroupDto>(ApiRoutes.ProjectGroup(group.Id), new UpdateProjectGroupRequest { Name = "Customers" });
        Assert.Equal("Customers", renamed.Name);

        var takenOut = await user.PutAsync<ProjectDto>(ApiRoutes.ProjectGroupOf(outside.Id), new SetProjectGroupRequest { ProjectGroupId = null });
        Assert.Null(takenOut.ProjectGroupId);

        var delete = await user.DeleteAsync(ApiRoutes.ProjectGroup(group.Id));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        // Deleting a group never deletes its projects.
        var survivor = await user.GetAsync<ProjectDto>(ApiRoutes.Project(inGroup.Id));
        var groups = await user.GetAsync<List<ProjectGroupDto>>(ApiRoutes.ProjectGroups);
        Assert.Null(survivor.ProjectGroupId);
        Assert.Empty(groups);
    }

    [Fact]
    public async Task Project_CannotBePlacedInAGroupThatDoesNotExist()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();
        var missingGroup = Guid.NewGuid();

        var create = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "Orphan", ProjectGroupId = missingGroup });
        var move = await user.PutAsync(ApiRoutes.ProjectGroupOf(project.Id), new SetProjectGroupRequest { ProjectGroupId = missingGroup });

        await create.ShouldBeAsync(HttpStatusCode.NotFound);
        await move.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Administrator_CanManageAProjectTheyAreNotAMemberOf()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var admin = scenario.Cast.Admin;

        var renamed = await admin.PutAsync<ProjectDto>(ApiRoutes.Project(scenario.Project.Id), new UpdateProjectRequest { Name = "Renamed by admin", IsActive = true });
        var task = await admin.CreateTaskAsync(scenario.Project.Id, "Added by admin");

        Assert.Equal("Renamed by admin", renamed.Name);
        Assert.Equal(scenario.Project.Id, task.ProjectId);

        // Acting on a project does not make the administrator its owner or a member.
        Assert.Equal(scenario.Cast.Owner.UserId, renamed.OwnerId);

        var members = await admin.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(scenario.Project.Id));
        Assert.DoesNotContain(members, member => member.UserId == admin.UserId);
    }
}
