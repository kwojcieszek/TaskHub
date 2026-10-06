using System.Net;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Dtos.UserSettings;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Two users with a project each: neither may see or touch what belongs to the other, also not by putting
/// a foreign identifier into an address or a request that is otherwise their own.
/// </summary>
public class DataIsolationTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public DataIsolationTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProjectList_ContainsOnlyTheCallersProjects()
    {
        var (mine, theirs) = await CreateTwoScenariosAsync();

        var projects = await mine.Cast.Stranger.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);

        Assert.Contains(projects, project => project.Id == theirs.Project.Id);
        Assert.DoesNotContain(projects, project => project.Id == mine.Project.Id);
    }

    [Fact]
    public async Task ProjectList_ContainsEverythingForAnAdministrator()
    {
        var (mine, theirs) = await CreateTwoScenariosAsync();

        var projects = await mine.Cast.Admin.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);

        Assert.Contains(projects, project => project.Id == mine.Project.Id);
        Assert.Contains(projects, project => project.Id == theirs.Project.Id);
    }

    [Fact]
    public async Task TaskLists_ContainOnlyTasksOfTheCallersProjects()
    {
        var (mine, theirs) = await CreateTwoScenariosAsync();
        var stranger = mine.Cast.Stranger;

        var all = await stranger.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);
        var calendar = await stranger.GetAsync<List<TaskItemDto>>($"{ApiRoutes.AllTasks}?from=2000-01-01&to=2100-01-01");

        Assert.Contains(all, task => task.ProjectId == theirs.Project.Id);
        Assert.DoesNotContain(all, task => task.ProjectId == mine.Project.Id);
        Assert.DoesNotContain(calendar, task => task.ProjectId == mine.Project.Id);
    }

    [Fact]
    public async Task ForeignProject_AndMissingProject_AreAnsweredTheSame()
    {
        // Otherwise the answer tells which project identifiers exist.
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var stranger = scenario.Cast.Stranger;

        var foreign = await stranger.GetAsync(ApiRoutes.Project(scenario.Project.Id));
        var missing = await stranger.GetAsync(ApiRoutes.Project(Guid.NewGuid()));

        Assert.Equal(foreign.StatusCode, missing.StatusCode);
        Assert.Equal(await foreign.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ForeignTask_CannotBeReachedThroughOwnProject()
    {
        var (mine, theirs) = await CreateTwoScenariosAsync();
        var attacker = mine.Cast.Stranger;
        var ownProject = theirs.Project.Id;
        var foreignTask = mine.LeafTask.Id;

        var get = await attacker.GetAsync(ApiRoutes.Task(ownProject, foreignTask));
        var subTasks = await attacker.GetAsync(ApiRoutes.SubTasks(ownProject, mine.ParentTask.Id));
        var update = await attacker.PutAsync(ApiRoutes.Task(ownProject, foreignTask), new UpdateTaskItemRequest { Title = "Hijacked", Status = TaskItemStatus.Done });
        var start = await attacker.PostAsync(ApiRoutes.StartTask(ownProject, foreignTask));
        var complete = await attacker.PostAsync(ApiRoutes.CompleteTask(ownProject, foreignTask));
        var delete = await attacker.DeleteAsync(ApiRoutes.Task(ownProject, foreignTask));

        await get.ShouldBeAsync(HttpStatusCode.NotFound);
        await subTasks.ShouldBeAsync(HttpStatusCode.NotFound);
        await update.ShouldBeAsync(HttpStatusCode.NotFound);
        await start.ShouldBeAsync(HttpStatusCode.NotFound);
        await complete.ShouldBeAsync(HttpStatusCode.NotFound);
        await delete.ShouldBeAsync(HttpStatusCode.NotFound);

        var untouched = await mine.Cast.Owner.GetAsync<TaskItemDto>(ApiRoutes.Task(mine.Project.Id, foreignTask));
        Assert.Equal(mine.LeafTask.Title, untouched.Title);
        Assert.Equal(TaskItemStatus.ToDo, untouched.Status);
    }

    [Fact]
    public async Task ForeignTask_CannotBecomeTheParentOfOwnTask()
    {
        var (mine, theirs) = await CreateTwoScenariosAsync();
        var attacker = mine.Cast.Stranger;
        var ownProject = theirs.Project.Id;
        var foreignParent = mine.ParentTask.Id;

        var viaRoute = await attacker.PostAsync(ApiRoutes.SubTasks(ownProject, foreignParent), new CreateTaskItemRequest { Title = "Planted" });
        var viaBody = await attacker.PostAsync(ApiRoutes.Tasks(ownProject), new CreateTaskItemRequest { Title = "Planted", ParentTaskId = foreignParent });
        var viaUpdate = await attacker.PutAsync(ApiRoutes.Task(ownProject, theirs.LeafTask.Id), new UpdateTaskItemRequest { Title = "Planted", ParentTaskId = foreignParent });

        await viaRoute.ShouldBeAsync(HttpStatusCode.NotFound);
        await viaBody.ShouldBeAsync(HttpStatusCode.NotFound);
        await viaUpdate.ShouldBeAsync(HttpStatusCode.NotFound);

        var subTasks = await mine.Cast.Owner.GetAsync<List<TaskItemDto>>(ApiRoutes.SubTasks(mine.Project.Id, foreignParent));
        Assert.Equal(mine.SubTask.Id, Assert.Single(subTasks).Id);
    }

    [Fact]
    public async Task TaskCreatedInOwnProject_CannotBePlacedInAForeignProject()
    {
        // The project comes from the address; a project id in the body must be ignored.
        var (mine, theirs) = await CreateTwoScenariosAsync();
        var attacker = mine.Cast.Stranger;

        var created = await attacker.PostAsync<TaskItemDto>(ApiRoutes.Tasks(theirs.Project.Id), new { title = "Misplaced", projectId = mine.Project.Id, id = Guid.NewGuid() });

        Assert.Equal(theirs.Project.Id, created.ProjectId);

        var foreignTasks = await mine.Cast.Owner.GetAsync<List<TaskItemDto>>(ApiRoutes.Tasks(mine.Project.Id));
        Assert.DoesNotContain(foreignTasks, task => task.Title == "Misplaced");
    }

    [Fact]
    public async Task ForeignAttachment_CannotBeReachedThroughOwnProject()
    {
        var (mine, theirs) = await CreateTwoScenariosAsync();
        var attacker = mine.Cast.Stranger;
        var ownProject = theirs.Project.Id;
        var ownTask = theirs.LeafTask.Id;

        var listByForeignTask = await attacker.GetAsync(ApiRoutes.Attachments(ownProject, mine.LeafTask.Id));
        var uploadToForeignTask = await attacker.UploadAsync(ApiRoutes.Attachments(ownProject, mine.LeafTask.Id), "planted.txt", new byte[] { 1, 2, 3 });
        var downloadViaOwnTask = await attacker.GetAsync(ApiRoutes.AttachmentContent(ownProject, ownTask, mine.Attachment.Id));
        var downloadViaForeignTask = await attacker.GetAsync(ApiRoutes.AttachmentContent(ownProject, mine.LeafTask.Id, mine.Attachment.Id));
        var deleteViaOwnTask = await attacker.DeleteAsync(ApiRoutes.Attachment(ownProject, ownTask, mine.Attachment.Id));
        var deleteViaForeignTask = await attacker.DeleteAsync(ApiRoutes.Attachment(ownProject, mine.LeafTask.Id, mine.Attachment.Id));

        await listByForeignTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await uploadToForeignTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await downloadViaOwnTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await downloadViaForeignTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await deleteViaOwnTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await deleteViaForeignTask.ShouldBeAsync(HttpStatusCode.NotFound);

        var attachments = await mine.Cast.Owner.GetAsync<List<AttachmentDto>>(ApiRoutes.Attachments(mine.Project.Id, mine.LeafTask.Id));
        Assert.Equal(mine.Attachment.Id, Assert.Single(attachments).Id);
    }

    [Fact]
    public async Task KnowingTheHashOfAForeignFile_DoesNotGiveAccessToIt()
    {
        var (mine, theirs) = await CreateTwoScenariosAsync();
        var attacker = mine.Cast.Stranger;
        var foreignHash = mine.Attachment.Sha256;

        var findViaForeignTask = await attacker.GetAsync(ApiRoutes.AttachmentsBySha256(foreignHash, mine.LeafTask.Id));
        var downloadViaForeignTask = await attacker.GetAsync(ApiRoutes.AttachmentContentBySha256(foreignHash, mine.LeafTask.Id));
        var findViaOwnTask = await attacker.GetAsync<List<AttachmentDto>>(ApiRoutes.AttachmentsBySha256(foreignHash, theirs.LeafTask.Id));
        var downloadViaOwnTask = await attacker.GetAsync(ApiRoutes.AttachmentContentBySha256(foreignHash, theirs.LeafTask.Id));
        var downloadViaMissingTask = await attacker.GetAsync(ApiRoutes.AttachmentContentBySha256(foreignHash, Guid.NewGuid()));

        await findViaForeignTask.ShouldBeAsync(HttpStatusCode.Forbidden);
        await downloadViaForeignTask.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.Empty(findViaOwnTask);
        await downloadViaOwnTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await downloadViaMissingTask.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadingTheSameContent_DoesNotExposeOrEndangerTheOtherCopy()
    {
        // Content is stored once per hash, so two users can share a stored file without knowing it.
        var (mine, theirs) = await CreateTwoScenariosAsync();
        var other = mine.Cast.Stranger;

        var copy = await other.UploadAttachmentAsync(theirs.Project.Id, theirs.LeafTask.Id, "copy.txt", mine.AttachmentContent);
        Assert.Equal(mine.Attachment.Sha256, copy.Sha256);

        var found = await other.GetAsync<List<AttachmentDto>>(ApiRoutes.AttachmentsBySha256(copy.Sha256, theirs.LeafTask.Id));
        Assert.Equal(copy.Id, Assert.Single(found).Id);

        var delete = await other.DeleteAsync(ApiRoutes.Attachment(theirs.Project.Id, theirs.LeafTask.Id, copy.Id));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var original = await mine.Cast.Owner.GetAsync(ApiRoutes.AttachmentContent(mine.Project.Id, mine.LeafTask.Id, mine.Attachment.Id));
        await original.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(mine.AttachmentContent, await original.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AttachmentUsage_CountsOnlyTheCallersFiles()
    {
        var owner = await _factory.CreateUserAsync("quota");
        var other = await _factory.CreateUserAsync("quota");
        var project = await owner.CreateProjectAsync();
        var task = await owner.CreateTaskAsync(project.Id);
        await owner.UploadAttachmentAsync(project.Id, task.Id, "file.bin", new byte[100]);

        var ownerLimits = await owner.GetAsync<AttachmentLimitsDto>(ApiRoutes.AttachmentLimits);
        var otherLimits = await other.GetAsync<AttachmentLimitsDto>(ApiRoutes.AttachmentLimits);

        Assert.Equal(100, ownerLimits.UsedBytes);
        Assert.Equal(0, otherLimits.UsedBytes);
    }

    [Fact]
    public async Task ProjectGroup_OfAnotherUser_IsInvisibleAndUntouchable()
    {
        var cast = await _factory.GetCastAsync();
        var group = await cast.Owner.CreateProjectGroupAsync($"Private {Guid.NewGuid():N}");
        var attacker = cast.Stranger;

        var list = await attacker.GetAsync<List<ProjectGroupDto>>(ApiRoutes.ProjectGroups);
        var get = await attacker.GetAsync(ApiRoutes.ProjectGroup(group.Id));
        var projects = await attacker.GetAsync<List<ProjectDto>>(ApiRoutes.ProjectGroupProjects(group.Id));
        var update = await attacker.PutAsync(ApiRoutes.ProjectGroup(group.Id), new UpdateProjectGroupRequest { Name = "Hijacked" });
        var delete = await attacker.DeleteAsync(ApiRoutes.ProjectGroup(group.Id));

        Assert.DoesNotContain(list, existing => existing.Id == group.Id);
        await get.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Empty(projects);
        await update.ShouldBeAsync(HttpStatusCode.Forbidden);
        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);

        var untouched = await cast.Owner.GetAsync<ProjectGroupDto>(ApiRoutes.ProjectGroup(group.Id));
        Assert.Equal(group.Name, untouched.Name);
    }

    [Fact]
    public async Task ProjectGroup_OfAnotherUser_CannotReceiveOwnProjects()
    {
        var cast = await _factory.GetCastAsync();
        var foreignGroup = await cast.Owner.CreateProjectGroupAsync($"Private {Guid.NewGuid():N}");
        var attacker = cast.Stranger;
        var ownProject = await attacker.CreateProjectAsync("Own project");

        var createInGroup = await attacker.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "Planted", ProjectGroupId = foreignGroup.Id });
        var moveIntoGroup = await attacker.PutAsync(ApiRoutes.ProjectGroupOf(ownProject.Id), new SetProjectGroupRequest { ProjectGroupId = foreignGroup.Id });

        await createInGroup.ShouldBeAsync(HttpStatusCode.Forbidden);
        await moveIntoGroup.ShouldBeAsync(HttpStatusCode.Forbidden);

        var group = await cast.Owner.GetAsync<ProjectGroupDto>(ApiRoutes.ProjectGroup(foreignGroup.Id));
        Assert.Equal(0, group.ProjectCount);
    }

    [Fact]
    public async Task ProjectGroup_ShowsAMemberOnlyTheProjectsTheyBelongTo()
    {
        var cast = await _factory.GetCastAsync();
        var owner = cast.Owner;
        var group = await owner.CreateProjectGroupAsync($"Shared {Guid.NewGuid():N}");
        var shared = await owner.CreateProjectAsync("Shared", group.Id);
        var secret = await owner.CreateProjectAsync("Secret", group.Id);
        await owner.AddMemberAsync(shared.Id, cast.Viewer.UserId, ProjectMemberRole.Viewer);

        var visibleGroup = await cast.Viewer.GetAsync<ProjectGroupDto>(ApiRoutes.ProjectGroup(group.Id));
        var visibleProjects = await cast.Viewer.GetAsync<List<ProjectDto>>(ApiRoutes.ProjectGroupProjects(group.Id));
        var update = await cast.Viewer.PutAsync(ApiRoutes.ProjectGroup(group.Id), new UpdateProjectGroupRequest { Name = "Hijacked" });
        var delete = await cast.Viewer.DeleteAsync(ApiRoutes.ProjectGroup(group.Id));

        Assert.Equal(group.Id, visibleGroup.Id);
        Assert.Equal(shared.Id, Assert.Single(visibleProjects).Id);
        Assert.DoesNotContain(visibleProjects, project => project.Id == secret.Id);
        await update.ShouldBeAsync(HttpStatusCode.Forbidden);
        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProjectOwner_CannotBeReplacedThroughTheRequestBody()
    {
        var cast = await _factory.GetCastAsync();
        var attacker = cast.Stranger;

        var created = await attacker.PostAsync<ProjectDto>(ApiRoutes.Projects, new { name = "Framed", ownerId = cast.Owner.UserId, id = Guid.NewGuid(), isActive = false }, HttpStatusCode.Created);

        Assert.Equal(attacker.UserId, created.OwnerId);
        Assert.True(created.IsActive);

        var updated = await attacker.PutAsync<ProjectDto>(ApiRoutes.Project(created.Id), new { name = "Framed", ownerId = cast.Owner.UserId, isActive = true });
        Assert.Equal(attacker.UserId, updated.OwnerId);

        var victimProjects = await cast.Owner.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        Assert.DoesNotContain(victimProjects, project => project.Id == created.Id);
    }

    [Fact]
    public async Task Member_CannotRaiseTheirOwnRole()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var viewer = scenario.Cast.Viewer;
        var editor = scenario.Cast.Editor;

        var viewerAddsSelf = await viewer.PostAsync(ApiRoutes.ProjectMembers(scenario.Project.Id), new AddProjectMemberRequest { UserId = viewer.UserId, Role = ProjectMemberRole.Owner });
        var editorAddsSelf = await editor.PostAsync(ApiRoutes.ProjectMembers(scenario.Project.Id), new AddProjectMemberRequest { UserId = editor.UserId, Role = ProjectMemberRole.Owner });
        var editorRemovesOwner = await editor.DeleteAsync(ApiRoutes.ProjectMember(scenario.Project.Id, scenario.Cast.Owner.UserId));

        await viewerAddsSelf.ShouldBeAsync(HttpStatusCode.Forbidden);
        await editorAddsSelf.ShouldBeAsync(HttpStatusCode.Forbidden);
        await editorRemovesOwner.ShouldBeAsync(HttpStatusCode.Forbidden);

        var members = await scenario.Cast.Owner.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(scenario.Project.Id));
        Assert.Equal(ProjectMemberRole.Viewer.ToString(), members.Single(member => member.UserId == viewer.UserId).Role);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), members.Single(member => member.UserId == editor.UserId).Role);
        Assert.Equal(ProjectMemberRole.Owner.ToString(), members.Single(member => member.UserId == scenario.Cast.Owner.UserId).Role);
    }

    [Fact]
    public async Task Owner_CannotBeRemovedFromTheirProject()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);

        var byOwner = await scenario.Cast.Owner.DeleteAsync(ApiRoutes.ProjectMember(scenario.Project.Id, scenario.Cast.Owner.UserId));
        var byAdmin = await scenario.Cast.Admin.DeleteAsync(ApiRoutes.ProjectMember(scenario.Project.Id, scenario.Cast.Owner.UserId));

        await byOwner.ShouldBeAsync(HttpStatusCode.Conflict);
        await byAdmin.ShouldBeAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UserSettings_ArePrivateToEachUser()
    {
        var first = await _factory.CreateUserAsync("settings");
        var second = await _factory.CreateUserAsync("settings");
        await first.PutAsync<UserSettingDto>(ApiRoutes.Setting("theme"), new SetUserSettingRequest { Value = "dark" });
        await second.PutAsync<UserSettingDto>(ApiRoutes.Setting("theme"), new SetUserSettingRequest { Value = "light" });
        await first.PutAsync<UserSettingDto>(ApiRoutes.Setting("private-note"), new SetUserSettingRequest { Value = "secret" });

        var secondList = await second.GetAsync<List<UserSettingDto>>(ApiRoutes.Settings);
        var secondReadsPrivate = await second.GetAsync(ApiRoutes.Setting("private-note"));
        var secondDeletesPrivate = await second.DeleteAsync(ApiRoutes.Setting("private-note"));

        Assert.Equal("light", Assert.Single(secondList).Value);
        await secondReadsPrivate.ShouldBeAsync(HttpStatusCode.NotFound);
        await secondDeletesPrivate.ShouldBeAsync(HttpStatusCode.NotFound);

        // Deleting the setting both users named the same removes only the caller's.
        var secondDeletesTheme = await second.DeleteAsync(ApiRoutes.Setting("theme"));
        await secondDeletesTheme.ShouldBeAsync(HttpStatusCode.NoContent);

        var firstTheme = await first.GetAsync<UserSettingDto>(ApiRoutes.Setting("theme"));
        var firstNote = await first.GetAsync<UserSettingDto>(ApiRoutes.Setting("private-note"));
        Assert.Equal("dark", firstTheme.Value);
        Assert.Equal("secret", firstNote.Value);
    }

    [Fact]
    public async Task UserSettings_IgnoreAUserIdSentWithTheRequest()
    {
        var attacker = await _factory.CreateUserAsync("settings");
        var victim = await _factory.CreateUserAsync("settings");

        var response = await attacker.PutAsync($"{ApiRoutes.Setting("theme")}?userId={victim.UserId}", new { value = "planted", userId = victim.UserId });
        await response.ShouldBeAsync(HttpStatusCode.OK);

        var victimSettings = await victim.GetAsync<List<UserSettingDto>>(ApiRoutes.Settings);
        Assert.Empty(victimSettings);
    }

    [Fact]
    public async Task UserDirectory_ShowsNamesButNoContactOrAccountDetails()
    {
        var user = await _factory.CreateUserAsync("directory");
        var admin = await _factory.AdminAsync();

        var response = await user.GetAsync($"{ApiRoutes.Users}?q={TaskHubApiFactory.AdminUserName}");
        var body = await response.Content.ReadAsStringAsync();

        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Contains(admin.UserId.ToString(), body);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(admin.Email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockedOut", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("twoFactor", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProjectMembers_ShowNoContactOrAccountDetails()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);

        var response = await scenario.Cast.Viewer.GetAsync(ApiRoutes.ProjectMembers(scenario.Project.Id));
        var body = await response.Content.ReadAsStringAsync();

        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@example.test", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(ProjectScenario Mine, ProjectScenario Theirs)> CreateTwoScenariosAsync()
    {
        // "Mine" belongs to the cast's owner. "Theirs" is a full project of the stranger, who is the attacker:
        // a real member with real rights elsewhere, which is exactly when a mixed-up identifier slips through.
        var mine = await ProjectScenario.CreateAsync(_factory);
        var stranger = mine.Cast.Stranger;

        var project = await stranger.CreateProjectAsync($"Theirs {Guid.NewGuid():N}");
        var parentTask = await stranger.CreateTaskAsync(project.Id, "Their parent");
        var subTask = await stranger.CreateSubTaskAsync(project.Id, parentTask.Id);
        var leafTask = await stranger.CreateTaskAsync(project.Id, "Their leaf", DateTime.UtcNow.AddDays(1));
        var content = Guid.NewGuid().ToByteArray();
        var attachment = await stranger.UploadAttachmentAsync(project.Id, leafTask.Id, "theirs.txt", content);

        var theirs = ProjectScenario.FromExisting(mine.Cast, project, parentTask, subTask, leafTask, attachment, content);

        return (mine, theirs);
    }
}
