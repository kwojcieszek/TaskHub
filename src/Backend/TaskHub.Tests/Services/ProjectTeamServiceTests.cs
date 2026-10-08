using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class ProjectTeamServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IProjectTeamService _teams;
    private readonly ITaskItemService _tasks;
    private readonly IProjectService _projects;

    public ProjectTeamServiceTests()
    {
        _teams = _host.Get<IProjectTeamService>();
        _tasks = _host.Get<ITaskItemService>();
        _projects = _host.Get<IProjectService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Add_GivesTheTeamARoleInTheProject()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var project = await _host.CreateProjectAsync(owner);
        var team = await _host.CreateTeamAsync("Developers", member);

        var added = await _teams.AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Editor });
        var listed = Assert.Single(await _teams.GetAsync(project.Id));

        Assert.Equal("Developers", added.Name);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), added.Role);
        Assert.Equal(1, added.MemberCount);
        Assert.Equal(team.Id, listed.UserGroupId);
    }

    [Fact]
    public async Task Add_RefusesTheOwnerRole_AnUndefinedRole_ADuplicateAndAMissingTeam()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner);
        var team = await _host.CreateTeamAsync("Developers");
        _host.SignIn(owner);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _teams.AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Owner }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _teams.AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = team.Id, Role = (ProjectMemberRole)99 }));
        await Assert.ThrowsAsync<NotFoundException>(() => _teams.AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = Guid.NewGuid(), Role = ProjectMemberRole.Viewer }));

        await _teams.AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Viewer });

        await Assert.ThrowsAsync<ConflictException>(() => _teams.AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Editor }));
        Assert.Equal(ProjectMemberRole.Viewer.ToString(), Assert.Single(await _teams.GetAsync(project.Id)).Role);
    }

    [Fact]
    public async Task OnlyWhoManagesTheProject_ChangesItsTeams()
    {
        var owner = await _host.CreateUserAsync("owner");
        var editor = await _host.CreateUserAsync("editor");
        var outsider = await _host.CreateUserAsync("outsider");
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, editor, ProjectMemberRole.Editor);
        var team = await _host.CreateTeamAsync("Developers");
        var request = new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Viewer };

        _host.SignIn(editor);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _teams.AddAsync(project.Id, request));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _teams.GetAvailableAsync(project.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _teams.RemoveAsync(project.Id, team.Id));
        Assert.Empty(await _teams.GetAsync(project.Id));

        _host.SignIn(outsider);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _teams.GetAsync(project.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _teams.AddAsync(project.Id, request));

        _host.SignIn(admin, Roles.Admin);
        await _teams.AddAsync(project.Id, request);
        await _teams.RemoveAsync(project.Id, team.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => _teams.RemoveAsync(project.Id, team.Id));
    }

    [Fact]
    public async Task Available_OffersTheTeamsThatAreNotInTheProjectYet()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner);
        var added = await _host.CreateTeamAsync("Added");
        var free = await _host.CreateTeamAsync("Free", owner);
        await _host.AddTeamAsync(project, owner, added, ProjectMemberRole.Viewer);

        var available = Assert.Single(await _teams.GetAvailableAsync(project.Id));

        Assert.Equal(free.Id, available.UserGroupId);
        Assert.Null(available.Role);
        Assert.Equal(1, available.MemberCount);
    }

    [Fact]
    public async Task ViewerTeam_Reads_EditorTeam_Edits_NeitherManages()
    {
        var owner = await _host.CreateUserAsync("owner");
        var reader = await _host.CreateUserAsync("reader");
        var writer = await _host.CreateUserAsync("writer");
        var project = await _host.CreateProjectAsync(owner);
        var task = await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Task" });
        await _host.AddTeamAsync(project, owner, await _host.CreateTeamAsync("Readers", reader), ProjectMemberRole.Viewer);
        await _host.AddTeamAsync(project, owner, await _host.CreateTeamAsync("Writers", writer), ProjectMemberRole.Editor);

        _host.SignIn(reader);
        Assert.Equal(task.Id, (await _tasks.GetByIdAsync(project.Id, task.Id)).Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Planted" }));

        _host.SignIn(writer);
        await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Allowed" });
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.DeleteAsync(project.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.AddMemberAsync(project.Id, new AddProjectMemberRequest { UserId = reader.Id, Role = ProjectMemberRole.Editor }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.UpdateAsync(project.Id, new UpdateProjectRequest { Name = project.Name, IsActive = false }));
    }

    [Fact]
    public async Task StrongestOfMembershipAndTeams_Applies()
    {
        var owner = await _host.CreateUserAsync("owner");
        var raised = await _host.CreateUserAsync("raised");
        var kept = await _host.CreateUserAsync("kept");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, raised, ProjectMemberRole.Viewer);
        await _host.AddMemberAsync(project, owner, kept, ProjectMemberRole.Editor);
        await _host.AddTeamAsync(project, owner, await _host.CreateTeamAsync("Editors", raised), ProjectMemberRole.Editor);
        await _host.AddTeamAsync(project, owner, await _host.CreateTeamAsync("Viewers", kept, raised), ProjectMemberRole.Viewer);

        _host.SignIn(raised);
        await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Raised by a team" });
        _host.SignIn(kept);
        await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Not lowered by a team" });
    }

    [Fact]
    public async Task TeamProject_IsListedForItsMembers()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var outsider = await _host.CreateUserAsync("outsider");
        var project = await _host.CreateProjectAsync(owner, "Shared");
        await _host.CreateProjectAsync(owner, "Private");
        await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Shared task", DueDate = DateTime.UtcNow.AddDays(1) });
        await _host.AddTeamAsync(project, owner, await _host.CreateTeamAsync("Developers", member), ProjectMemberRole.Viewer);

        _host.SignIn(member);
        var projects = await _projects.GetAccessibleProjectsAsync();
        var tasks = await _tasks.GetMyTasksAsync();
        var calendar = await _tasks.GetCalendarAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));

        _host.SignIn(outsider);
        var outsiderProjects = await _projects.GetAccessibleProjectsAsync();
        var outsiderTasks = await _tasks.GetMyTasksAsync();

        Assert.Equal(project.Id, Assert.Single(projects).Id);
        Assert.Single(tasks);
        Assert.Single(calendar);
        Assert.Empty(outsiderProjects);
        Assert.Empty(outsiderTasks);
    }

    [Fact]
    public async Task Members_IncludeTeamMembersWithTheRoleTheTeamGives()
    {
        var owner = await _host.CreateUserAsync("owner");
        var direct = await _host.CreateUserAsync("direct");
        var viaTeam = await _host.CreateUserAsync("viateam");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, direct, ProjectMemberRole.Viewer);
        await _host.AddTeamAsync(project, owner, await _host.CreateTeamAsync("Editors", direct, viaTeam), ProjectMemberRole.Editor);

        var members = await _projects.GetMembersAsync(project.Id);

        var ownerEntry = members.Single(member => member.UserId == owner.Id);
        var directEntry = members.Single(member => member.UserId == direct.Id);
        var teamEntry = members.Single(member => member.UserId == viaTeam.Id);

        Assert.Equal(3, members.Count);
        Assert.True(ownerEntry.Direct);
        Assert.Empty(ownerEntry.Teams);
        Assert.True(directEntry.Direct);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), directEntry.Role);
        Assert.Equal(new[] { "Editors" }, directEntry.Teams);
        Assert.False(teamEntry.Direct);
        Assert.Equal(ProjectMemberRole.Editor.ToString(), teamEntry.Role);
    }

    [Fact]
    public async Task TeamMember_CanBeAssignedATask_AndSomeoneOutsideCannot()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var outsider = await _host.CreateUserAsync("outsider");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddTeamAsync(project, owner, await _host.CreateTeamAsync("Developers", member), ProjectMemberRole.Viewer);

        var assigned = await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "For the team", AssignedUserId = member.Id });

        Assert.Equal(member.Id, assigned.AssignedUserId);
        await Assert.ThrowsAsync<ValidationFailedException>(() => _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "For an outsider", AssignedUserId = outsider.Id }));
    }

    [Fact]
    public async Task Access_Ends_WhenTheTeamOrTheMembershipIsGone()
    {
        var owner = await _host.CreateUserAsync("owner");
        var leaving = await _host.CreateUserAsync("leaving");
        var staying = await _host.CreateUserAsync("staying");
        var project = await _host.CreateProjectAsync(owner);
        var team = await _host.CreateTeamAsync("Developers", leaving, staying);
        await _host.AddTeamAsync(project, owner, team, ProjectMemberRole.Editor);
        var groups = _host.Get<IUserGroupService>();

        await groups.RemoveMemberAsync(team.Id, leaving.Id);

        _host.SignIn(leaving);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.GetByIdAsync(project.Id));
        _host.SignIn(staying);
        await _projects.GetByIdAsync(project.Id);

        _host.SignIn(owner);
        await _teams.RemoveAsync(project.Id, team.Id);

        _host.SignIn(staying);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.GetByIdAsync(project.Id));
        Assert.Empty(await _projects.GetAccessibleProjectsAsync());
    }

    [Fact]
    public async Task DeletingTheTeam_TakesItOutOfEveryProject()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var project = await _host.CreateProjectAsync(owner);
        var team = await _host.CreateTeamAsync("Developers", member);
        await _host.AddTeamAsync(project, owner, team, ProjectMemberRole.Editor);

        await _host.Get<IUserGroupService>().DeleteAsync(team.Id);

        Assert.Empty(await _teams.GetAsync(project.Id));
        _host.SignIn(member);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _projects.GetByIdAsync(project.Id));
    }
}
