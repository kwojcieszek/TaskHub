using System.Net;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// A team (a user group) added to a project gives its members the team's role there, and nothing beyond it:
/// not in other projects, not after the team or the member is removed, and never the right to manage.
/// </summary>
public class ProjectTeamAccessTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public ProjectTeamAccessTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ViewerTeam_LetsItsMembersReadButNotChange()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var member = await _factory.CreateUserAsync("teamviewer");
        var project = await owner.CreateProjectAsync("Shared with a team");
        var task = await owner.CreateTaskAsync(project.Id, "Team task");
        var team = await CreateTeamAsync(member);

        var before = await member.GetAsync(ApiRoutes.Project(project.Id));
        await owner.AddTeamAsync(project.Id, team.Id, ProjectMemberRole.Viewer);

        var read = await member.GetAsync<TaskItemDto>(ApiRoutes.Task(project.Id, task.Id));
        var create = await member.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "Planted" });
        var update = await member.PutAsync(ApiRoutes.Task(project.Id, task.Id), new UpdateTaskItemRequest { Title = "Hijacked" });
        var delete = await member.DeleteAsync(ApiRoutes.Task(project.Id, task.Id));
        var upload = await member.UploadAsync(ApiRoutes.Attachments(project.Id, task.Id), "planted.txt", new byte[] { 1 });

        await before.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.Equal(task.Title, read.Title);
        await create.ShouldBeAsync(HttpStatusCode.Forbidden);
        await update.ShouldBeAsync(HttpStatusCode.Forbidden);
        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);
        await upload.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditorTeam_LetsItsMembersEditButNotManage()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var member = await _factory.CreateUserAsync("teameditor");
        var outsider = await _factory.CreateUserAsync("teamoutsider");
        var project = await owner.CreateProjectAsync("Shared with editors");
        var team = await CreateTeamAsync(member);
        var otherTeam = await CreateTeamAsync(outsider);
        await owner.AddTeamAsync(project.Id, team.Id, ProjectMemberRole.Editor);

        var created = await member.CreateTaskAsync(project.Id, "Created through a team");
        var rename = await member.PutAsync(ApiRoutes.Project(project.Id), new UpdateProjectRequest { Name = "Renamed by the team", IsActive = true });

        var deactivate = await member.PutAsync(ApiRoutes.Project(project.Id), new UpdateProjectRequest { Name = "Renamed by the team", IsActive = false });
        var deleteProject = await member.DeleteAsync(ApiRoutes.Project(project.Id));
        var addMember = await member.PostAsync(ApiRoutes.ProjectMembers(project.Id), new AddProjectMemberRequest { UserId = outsider.UserId, Role = ProjectMemberRole.Viewer });
        var makeSelfOwner = await member.PostAsync(ApiRoutes.ProjectMembers(project.Id), new AddProjectMemberRequest { UserId = member.UserId, Role = ProjectMemberRole.Owner });
        var removeOwner = await member.DeleteAsync(ApiRoutes.ProjectMember(project.Id, owner.UserId));
        var addTeam = await member.PostAsync(ApiRoutes.ProjectTeams(project.Id), new AddProjectTeamRequest { UserGroupId = otherTeam.Id, Role = ProjectMemberRole.Editor });
        var removeTeam = await member.DeleteAsync(ApiRoutes.ProjectTeam(project.Id, team.Id));
        var available = await member.GetAsync(ApiRoutes.AvailableProjectTeams(project.Id));

        Assert.Equal(project.Id, created.ProjectId);
        await rename.ShouldBeAsync(HttpStatusCode.OK);
        await deactivate.ShouldBeAsync(HttpStatusCode.Forbidden);
        await deleteProject.ShouldBeAsync(HttpStatusCode.Forbidden);
        await addMember.ShouldBeAsync(HttpStatusCode.Forbidden);
        await makeSelfOwner.ShouldBeAsync(HttpStatusCode.Forbidden);
        await removeOwner.ShouldBeAsync(HttpStatusCode.Forbidden);
        await addTeam.ShouldBeAsync(HttpStatusCode.Forbidden);
        await removeTeam.ShouldBeAsync(HttpStatusCode.Forbidden);
        await available.ShouldBeAsync(HttpStatusCode.Forbidden);

        var outsiderRead = await outsider.GetAsync(ApiRoutes.Project(project.Id));
        await outsiderRead.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TeamProject_AppearsInTheListsOfItsMembersOnly()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var member = await _factory.CreateUserAsync("teamlists");
        var outsider = await _factory.CreateUserAsync("teamoutsider");
        var group = await owner.CreateProjectGroupAsync($"Team group {Guid.NewGuid():N}");
        var project = await owner.CreateProjectAsync("Listed for the team", group.Id);
        var hidden = await owner.CreateProjectAsync("Not shared", group.Id);
        var task = await owner.CreateTaskAsync(project.Id, "Listed task", DateTime.UtcNow.AddDays(1));
        await owner.CreateTaskAsync(hidden.Id, "Hidden task", DateTime.UtcNow.AddDays(1));
        var team = await CreateTeamAsync(member);
        await owner.AddTeamAsync(project.Id, team.Id, ProjectMemberRole.Viewer);

        var projects = await member.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        var tasks = await member.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);
        var calendar = await member.GetAsync<List<TaskItemDto>>($"{ApiRoutes.AllTasks}?from=2000-01-01&to=2100-01-01");
        var groupProjects = await member.GetAsync<List<ProjectDto>>(ApiRoutes.ProjectGroupProjects(group.Id));
        var outsiderProjects = await outsider.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        var outsiderTasks = await outsider.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);

        Assert.Equal(project.Id, Assert.Single(projects).Id);
        Assert.Equal(task.Id, Assert.Single(tasks).Id);
        Assert.Equal(task.Id, Assert.Single(calendar).Id);
        Assert.Equal(project.Id, Assert.Single(groupProjects).Id);
        Assert.Empty(outsiderProjects);
        Assert.Empty(outsiderTasks);
    }

    [Fact]
    public async Task Access_Ends_WhenTheTeamLeavesTheProject()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var member = await _factory.CreateUserAsync("teamremoved");
        var project = await owner.CreateProjectAsync("Shared for a while");
        var task = await owner.CreateTaskAsync(project.Id);
        var team = await CreateTeamAsync(member);
        await owner.AddTeamAsync(project.Id, team.Id, ProjectMemberRole.Editor);

        var whileShared = await member.GetAsync(ApiRoutes.Task(project.Id, task.Id));
        var removal = await owner.DeleteAsync(ApiRoutes.ProjectTeam(project.Id, team.Id));
        var afterRemoval = await member.GetAsync(ApiRoutes.Task(project.Id, task.Id));
        var update = await member.PutAsync(ApiRoutes.Task(project.Id, task.Id), new UpdateTaskItemRequest { Title = "Hijacked" });
        var projects = await member.GetAsync<List<ProjectDto>>(ApiRoutes.Projects);
        var removeAgain = await owner.DeleteAsync(ApiRoutes.ProjectTeam(project.Id, team.Id));

        await whileShared.ShouldBeAsync(HttpStatusCode.OK);
        await removal.ShouldBeAsync(HttpStatusCode.NoContent);
        await afterRemoval.ShouldBeAsync(HttpStatusCode.Forbidden);
        await update.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.Empty(projects);
        await removeAgain.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Access_Ends_WhenTheMemberLeavesTheTeam_OrTheTeamIsDeleted()
    {
        var admin = await _factory.AdminAsync();
        var owner = await _factory.CreateUserAsync("teamowner");
        var leaving = await _factory.CreateUserAsync("teamleaving");
        var staying = await _factory.CreateUserAsync("teamstaying");
        var project = await owner.CreateProjectAsync("Shared with a changing team");
        var team = await CreateTeamAsync(leaving, staying);
        await owner.AddTeamAsync(project.Id, team.Id, ProjectMemberRole.Editor);

        var leave = await admin.DeleteAsync(ApiRoutes.UserGroupMember(team.Id, leaving.UserId));
        await leave.ShouldBeAsync(HttpStatusCode.NoContent);

        var leavingRead = await leaving.GetAsync(ApiRoutes.Project(project.Id));
        var stayingRead = await staying.GetAsync(ApiRoutes.Project(project.Id));

        var deleteTeam = await admin.DeleteAsync(ApiRoutes.UserGroup(team.Id));
        await deleteTeam.ShouldBeAsync(HttpStatusCode.NoContent);

        var stayingAfterDelete = await staying.GetAsync(ApiRoutes.Project(project.Id));
        var teams = await owner.GetAsync<List<ProjectTeamDto>>(ApiRoutes.ProjectTeams(project.Id));

        await leavingRead.ShouldBeAsync(HttpStatusCode.Forbidden);
        await stayingRead.ShouldBeAsync(HttpStatusCode.OK);
        await stayingAfterDelete.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.Empty(teams);
    }

    [Fact]
    public async Task StrongestRole_OfMembershipAndTeams_Applies()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var raised = await _factory.CreateUserAsync("teamraised");
        var kept = await _factory.CreateUserAsync("teamkept");
        var project = await owner.CreateProjectAsync("Mixed roles");
        await owner.AddMemberAsync(project.Id, raised.UserId, ProjectMemberRole.Viewer);
        await owner.AddMemberAsync(project.Id, kept.UserId, ProjectMemberRole.Editor);
        var editors = await CreateTeamAsync(raised);
        var viewers = await CreateTeamAsync(kept, raised);

        var beforeTeam = await raised.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "Too early" });
        await owner.AddTeamAsync(project.Id, editors.Id, ProjectMemberRole.Editor);
        await owner.AddTeamAsync(project.Id, viewers.Id, ProjectMemberRole.Viewer);

        var raisedCreates = await raised.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "Raised by the team" });
        var keptCreates = await kept.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "Not lowered by the team" });
        var members = await owner.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(project.Id));

        await beforeTeam.ShouldBeAsync(HttpStatusCode.Forbidden);
        await raisedCreates.ShouldBeAsync(HttpStatusCode.OK);
        await keptCreates.ShouldBeAsync(HttpStatusCode.OK);

        var raisedEntry = members.Single(member => member.UserId == raised.UserId);
        var keptEntry = members.Single(member => member.UserId == kept.UserId);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), raisedEntry.Role);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), keptEntry.Role);
        Assert.True(raisedEntry.Direct);
        Assert.Equal(2, raisedEntry.Teams.Count);
    }

    [Fact]
    public async Task TeamMembers_AreListedInTheProject_AndCanBeAssignedTasks()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var member = await _factory.CreateUserAsync("teamassignee");
        var outsider = await _factory.CreateUserAsync("teamoutsider");
        var project = await owner.CreateProjectAsync("Team assignments");
        var team = await CreateTeamAsync(member);
        await owner.AddTeamAsync(project.Id, team.Id, ProjectMemberRole.Viewer);

        var members = await owner.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(project.Id));
        var assigned = await owner.PostAsync<TaskItemDto>(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "For the team member", AssignedUserId = member.UserId });
        var toOutsider = await owner.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "For an outsider", AssignedUserId = outsider.UserId });

        var entry = members.Single(listed => listed.UserId == member.UserId);
        Assert.False(entry.Direct);
        Assert.Equal(ProjectMemberRole.Viewer.ToString(), entry.Role);
        Assert.Equal(team.Name, Assert.Single(entry.Teams));
        Assert.True(members.Single(listed => listed.UserId == owner.UserId).Direct);
        Assert.Equal(member.UserId, assigned.AssignedUserId);
        await toOutsider.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Team_GivesNothingInOtherProjects()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var member = await _factory.CreateUserAsync("teamscoped");
        var shared = await owner.CreateProjectAsync("Shared");
        var secret = await owner.CreateProjectAsync("Secret");
        var secretTask = await owner.CreateTaskAsync(secret.Id, "Secret task");
        var team = await CreateTeamAsync(member);
        await owner.AddTeamAsync(shared.Id, team.Id, ProjectMemberRole.Editor);

        var direct = await member.GetAsync(ApiRoutes.Task(secret.Id, secretTask.Id));
        var throughShared = await member.GetAsync(ApiRoutes.Task(shared.Id, secretTask.Id));
        var move = await member.PutAsync(ApiRoutes.Task(shared.Id, secretTask.Id), new UpdateTaskItemRequest { Title = "Moved" });
        var teamsOfSecret = await member.GetAsync(ApiRoutes.ProjectTeams(secret.Id));

        await direct.ShouldBeAsync(HttpStatusCode.Forbidden);
        await throughShared.ShouldBeAsync(HttpStatusCode.NotFound);
        await move.ShouldBeAsync(HttpStatusCode.NotFound);
        await teamsOfSecret.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OnlyTheOwnerOrAnAdministrator_ManagesTheTeamsOfAProject()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var cast = scenario.Cast;
        var team = await CreateTeamAsync(await _factory.CreateUserAsync("teammember"));
        var request = new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Viewer };

        foreach (var caller in new[] { cast.Editor, cast.Viewer, cast.Stranger })
        {
            var add = await caller.PostAsync(ApiRoutes.ProjectTeams(scenario.Project.Id), request);
            var offered = await caller.GetAsync(ApiRoutes.AvailableProjectTeams(scenario.Project.Id));
            var remove = await caller.DeleteAsync(ApiRoutes.ProjectTeam(scenario.Project.Id, team.Id));

            await add.ShouldBeAsync(HttpStatusCode.Forbidden);
            await offered.ShouldBeAsync(HttpStatusCode.Forbidden);
            await remove.ShouldBeAsync(HttpStatusCode.Forbidden);
        }

        var anonymous = await cast.Anonymous.PostAsync(ApiRoutes.ProjectTeams(scenario.Project.Id), request);
        var viewerList = await cast.Viewer.GetAsync(ApiRoutes.ProjectTeams(scenario.Project.Id));
        var strangerList = await cast.Stranger.GetAsync(ApiRoutes.ProjectTeams(scenario.Project.Id));
        var available = await cast.Owner.GetAsync<List<ProjectTeamDto>>(ApiRoutes.AvailableProjectTeams(scenario.Project.Id));
        var byOwner = await cast.Owner.PostAsync<ProjectTeamDto>(ApiRoutes.ProjectTeams(scenario.Project.Id), request);
        var byAdmin = await cast.Admin.DeleteAsync(ApiRoutes.ProjectTeam(scenario.Project.Id, team.Id));

        await anonymous.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await viewerList.ShouldBeAsync(HttpStatusCode.OK);
        await strangerList.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.Contains(available, candidate => candidate.UserGroupId == team.Id);
        Assert.Equal(ProjectMemberRole.Viewer.ToString(), byOwner.Role);
        Assert.Equal(1, byOwner.MemberCount);
        await byAdmin.ShouldBeAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Team_CannotBeAddedAsOwner_Twice_OrWhenItDoesNotExist()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var project = await owner.CreateProjectAsync("Team rules");
        var team = await CreateTeamAsync(await _factory.CreateUserAsync("teammember"));

        var asOwner = await owner.PostAsync(ApiRoutes.ProjectTeams(project.Id), new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Owner });
        var undefinedRole = await owner.PostAsync(ApiRoutes.ProjectTeams(project.Id), new { userGroupId = team.Id, role = 99 });
        var missingTeam = await owner.PostAsync(ApiRoutes.ProjectTeams(project.Id), new AddProjectTeamRequest { UserGroupId = Guid.NewGuid(), Role = ProjectMemberRole.Viewer });
        var first = await owner.PostAsync(ApiRoutes.ProjectTeams(project.Id), new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Viewer });
        var second = await owner.PostAsync(ApiRoutes.ProjectTeams(project.Id), new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Editor });
        var available = await owner.GetAsync<List<ProjectTeamDto>>(ApiRoutes.AvailableProjectTeams(project.Id));

        await asOwner.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await undefinedRole.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await missingTeam.ShouldBeAsync(HttpStatusCode.NotFound);
        await first.ShouldBeAsync(HttpStatusCode.OK);
        await second.ShouldBeAsync(HttpStatusCode.Conflict);
        Assert.DoesNotContain(available, offered => offered.UserGroupId == team.Id);

        var teams = await owner.GetAsync<List<ProjectTeamDto>>(ApiRoutes.ProjectTeams(project.Id));
        Assert.Equal(ProjectMemberRole.Viewer.ToString(), Assert.Single(teams).Role);
    }

    [Fact]
    public async Task OnlyAnAdministrator_RenamesATeam()
    {
        var admin = await _factory.AdminAsync();
        var owner = await _factory.CreateUserAsync("teamowner");
        var member = await _factory.CreateUserAsync("teammember");
        var project = await owner.CreateProjectAsync("Renamed team");
        var team = await CreateTeamAsync(member);
        await owner.AddTeamAsync(project.Id, team.Id, ProjectMemberRole.Editor);
        var request = new UpdateUserGroupRequest { Name = "Hijacked" };

        var byOwner = await owner.PutAsync(ApiRoutes.UserGroup(team.Id), request);
        var byMember = await member.PutAsync(ApiRoutes.UserGroup(team.Id), request);
        var byAnonymous = await _factory.Anonymous().PutAsync(ApiRoutes.UserGroup(team.Id), request);
        var blank = await admin.PutAsync(ApiRoutes.UserGroup(team.Id), new UpdateUserGroupRequest { Name = "   " });
        var missing = await admin.PutAsync(ApiRoutes.UserGroup(Guid.NewGuid()), new UpdateUserGroupRequest { Name = "Ghosts" });
        var unchanged = Assert.Single(await owner.GetAsync<List<ProjectTeamDto>>(ApiRoutes.ProjectTeams(project.Id)));
        var renamed = await admin.PutAsync<UserGroupDto>(ApiRoutes.UserGroup(team.Id), new UpdateUserGroupRequest { Name = "Engineers", Description = "Renamed" });
        var inProject = Assert.Single(await owner.GetAsync<List<ProjectTeamDto>>(ApiRoutes.ProjectTeams(project.Id)));
        var stillMember = await member.GetAsync(ApiRoutes.Project(project.Id));

        await byOwner.ShouldBeAsync(HttpStatusCode.Forbidden);
        await byMember.ShouldBeAsync(HttpStatusCode.Forbidden);
        await byAnonymous.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await blank.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await missing.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Equal(team.Name, unchanged.Name);
        Assert.Equal("Engineers", renamed.Name);
        Assert.Equal(1, renamed.MemberCount);
        Assert.Equal("Engineers", inProject.Name);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), inProject.Role);
        await stillMember.ShouldBeAsync(HttpStatusCode.OK);
    }

    private async Task<UserGroupDto> CreateTeamAsync(params ApiSession[] members)
    {
        var admin = await _factory.AdminAsync();
        var team = await admin.PostAsync<UserGroupDto>(ApiRoutes.UserGroups, new CreateUserGroupRequest { Name = $"Team {Guid.NewGuid():N}" });

        foreach (var member in members)
        {
            var added = await admin.PostAsync(ApiRoutes.UserGroupMembers(team.Id), new AddUserGroupMemberRequest { UserId = member.UserId });
            await added.ShouldBeAsync(HttpStatusCode.OK);
        }

        return team;
    }
}
