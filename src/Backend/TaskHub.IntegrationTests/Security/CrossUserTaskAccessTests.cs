using System.Net;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Several users, each with a private project and tasks of their own. Every user tries every attack on every
/// other user: the attempt must be refused, reveal nothing and leave the tasks of both exactly as they were.
/// </summary>
public class CrossUserTaskAccessTests : IClassFixture<TaskOwnersFixture>
{
    private readonly TaskOwnersFixture _fixture;

    public CrossUserTaskAccessTests(TaskOwnersFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> Cases => CrossUserAttacks.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Attack_ByEveryUserOnEveryOther_IsRefusedAndChangesNothing(string attackName)
    {
        // One combination of identifiers, tried for every arrangement of the users as attacker and victim,
        // and also for every choice of the third user when the combination uses one.
        var attack = CrossUserAttacks.Get(attackName);
        var failures = new List<string>();

        foreach (var context in Arrangements(attack.InvolvesThird))
        {
            var response = await attack.Execute(context);
            var body = await response.Content.ReadAsStringAsync();
            var request = $"{context.Attacker.Session.UserName} against {context.Victim.Session.UserName}: {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}";

            if (response.StatusCode != attack.Expected)
            {
                failures.Add($"{request} returned {(int)response.StatusCode} {response.StatusCode}, expected {(int)attack.Expected} {attack.Expected}. Body: {body}");
            }

            if (attack.ExpectedBody is not null && body != attack.ExpectedBody)
            {
                failures.Add($"{request} returned the body {body}, expected {attack.ExpectedBody}");
            }

            if (body.Contains(context.Victim.Secret) || body.Contains(context.Third.Secret))
            {
                failures.Add($"{request} revealed data of another user. Body: {body}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

        foreach (var owner in _fixture.Owners)
        {
            await AssertUntouchedAsync(owner);

            var members = await owner.Session.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(owner.Project.Id));
            Assert.Equal(owner.Session.UserId, Assert.Single(members).UserId);
        }
    }

    [Fact]
    public async Task EveryUser_ListsOnlyTheirOwnProjectsAndTasks()
    {
        foreach (var owner in _fixture.Owners)
        {
            var others = _fixture.Owners.Where(other => other != owner).ToList();

            var projects = await owner.Session.GetAsync(ApiRoutes.Projects);
            var tasks = await owner.Session.GetAsync(ApiRoutes.AllTasks);
            var calendar = await owner.Session.GetAsync($"{ApiRoutes.AllTasks}?from=2000-01-01&to=2100-01-01");

            var projectList = await projects.ReadAsync<List<ProjectDto>>();
            var taskList = await tasks.ReadAsync<List<TaskItemDto>>();
            var calendarList = await calendar.ReadAsync<List<TaskItemDto>>();

            Assert.Equal(owner.Project.Id, Assert.Single(projectList).Id);
            Assert.Contains(owner.OwnTask.Id, Flatten(taskList).Select(task => task.Id));
            Assert.Contains(owner.OwnTask.Id, Flatten(calendarList).Select(task => task.Id));
            Assert.All(Flatten(taskList), task => Assert.Equal(owner.Project.Id, task.ProjectId));
            Assert.All(Flatten(calendarList), task => Assert.Equal(owner.Project.Id, task.ProjectId));

            foreach (var response in new[] { projects, tasks, calendar })
            {
                var body = await response.Content.ReadAsStringAsync();

                Assert.All(others, other => Assert.DoesNotContain(other.Secret, body));
            }
        }
    }

    [Fact]
    public async Task UserDirectory_DoesNotRevealTheTasksOrProjectsOfOthers()
    {
        var attacker = _fixture.Owners[0];

        foreach (var victim in _fixture.Owners.Skip(1))
        {
            var response = await attacker.Session.GetAsync($"{ApiRoutes.Users}?q={victim.Session.UserName}");
            var body = await response.Content.ReadAsStringAsync();

            await response.ShouldBeAsync(HttpStatusCode.OK);
            Assert.DoesNotContain(victim.Secret, body);
            Assert.DoesNotContain(victim.Project.Id.ToString(), body);
        }
    }

    [Fact]
    public async Task Viewer_OfOneUsersProject_ReadsItsTasksButChangesNone()
    {
        var owner = await _fixture.CreateOwnerAsync("shareowner");
        var viewer = await _fixture.CreateOwnerAsync("shareviewer");
        var outsider = await _fixture.CreateOwnerAsync("shareoutsider");
        await owner.Session.AddMemberAsync(owner.Project.Id, viewer.Session.UserId, ProjectMemberRole.Viewer);

        var read = await viewer.Session.GetAsync<TaskItemDto>(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id));
        var download = await viewer.Session.GetAsync(ApiRoutes.AttachmentContent(owner.Project.Id, owner.OwnTask.Id, owner.Attachment.Id));
        var update = await viewer.Session.PutAsync(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id), new UpdateTaskItemRequest { Title = "Hijacked" });
        var takeOver = await viewer.Session.PutAsync(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id), new UpdateTaskItemRequest { Title = owner.OwnTask.Title, AssignedUserId = viewer.Session.UserId });
        var complete = await viewer.Session.PostAsync(ApiRoutes.CompleteTask(owner.Project.Id, owner.OwnTask.Id));
        var create = await viewer.Session.PostAsync(ApiRoutes.Tasks(owner.Project.Id), new CreateTaskItemRequest { Title = "Planted" });
        var upload = await viewer.Session.UploadAsync(ApiRoutes.Attachments(owner.Project.Id, owner.OwnTask.Id), "planted.txt", new byte[] { 1, 2, 3 });
        var deleteAttachment = await viewer.Session.DeleteAsync(ApiRoutes.Attachment(owner.Project.Id, owner.OwnTask.Id, owner.Attachment.Id));
        var delete = await viewer.Session.DeleteAsync(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id));
        var inviteOutsider = await viewer.Session.PostAsync(ApiRoutes.ProjectMembers(owner.Project.Id), new AddProjectMemberRequest { UserId = outsider.Session.UserId, Role = ProjectMemberRole.Viewer });

        Assert.Equal(owner.OwnTask.Title, read.Title);
        await download.ShouldBeAsync(HttpStatusCode.OK);
        await update.ShouldBeAsync(HttpStatusCode.Forbidden);
        await takeOver.ShouldBeAsync(HttpStatusCode.Forbidden);
        await complete.ShouldBeAsync(HttpStatusCode.Forbidden);
        await create.ShouldBeAsync(HttpStatusCode.Forbidden);
        await upload.ShouldBeAsync(HttpStatusCode.Forbidden);
        await deleteAttachment.ShouldBeAsync(HttpStatusCode.Forbidden);
        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);
        await inviteOutsider.ShouldBeAsync(HttpStatusCode.Forbidden);

        await AssertUntouchedAsync(owner);
    }

    [Fact]
    public async Task SharingAProject_OpensNothingElse()
    {
        // The guest gets into the shared project only: not into the owner's other project, not the other way
        // round, and a third user gains nothing from it.
        var owner = await _fixture.CreateOwnerAsync("shareowner");
        var guest = await _fixture.CreateOwnerAsync("shareguest");
        var outsider = await _fixture.CreateOwnerAsync("shareoutsider");
        var privateProject = await owner.Session.CreateProjectAsync($"Private {owner.Secret}");
        var privateTask = await owner.Session.CreateTaskAsync(privateProject.Id, $"Private {owner.Secret}");
        await owner.Session.AddMemberAsync(owner.Project.Id, guest.Session.UserId, ProjectMemberRole.Editor);

        var guestReadsPrivate = await guest.Session.GetAsync(ApiRoutes.Task(privateProject.Id, privateTask.Id));
        var guestReadsPrivateThroughShared = await guest.Session.GetAsync(ApiRoutes.Task(owner.Project.Id, privateTask.Id));
        var guestMovesSharedIntoOwn = await guest.Session.PutAsync(ApiRoutes.Task(guest.Project.Id, owner.OwnTask.Id), new UpdateTaskItemRequest { Title = "Moved" });
        var guestMovesSharedUnderOwnParent = await guest.Session.PutAsync(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id), new UpdateTaskItemRequest { Title = owner.OwnTask.Title, AssignedUserId = owner.Session.UserId, ParentTaskId = guest.ParentTask.Id });
        var ownerReadsGuests = await owner.Session.GetAsync(ApiRoutes.Task(guest.Project.Id, guest.OwnTask.Id));
        var outsiderReadsShared = await outsider.Session.GetAsync(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id));
        var guestInvitesOutsider = await guest.Session.PostAsync(ApiRoutes.ProjectMembers(owner.Project.Id), new AddProjectMemberRequest { UserId = outsider.Session.UserId, Role = ProjectMemberRole.Viewer });
        var guestTasks = await guest.Session.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);

        await guestReadsPrivate.ShouldBeAsync(HttpStatusCode.Forbidden);
        await guestReadsPrivateThroughShared.ShouldBeAsync(HttpStatusCode.NotFound);
        await guestMovesSharedIntoOwn.ShouldBeAsync(HttpStatusCode.NotFound);
        await guestMovesSharedUnderOwnParent.ShouldBeAsync(HttpStatusCode.NotFound);
        await ownerReadsGuests.ShouldBeAsync(HttpStatusCode.Forbidden);
        await outsiderReadsShared.ShouldBeAsync(HttpStatusCode.Forbidden);
        await guestInvitesOutsider.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.DoesNotContain(Flatten(guestTasks), task => task.ProjectId == privateProject.Id);

        await AssertUntouchedAsync(owner);
        await AssertUntouchedAsync(guest);
    }

    [Fact]
    public async Task Editor_LosesEveryAccess_WhenRemovedFromTheProject()
    {
        var owner = await _fixture.CreateOwnerAsync("revokeowner");
        var editor = await _fixture.CreateOwnerAsync("revokeeditor");
        await owner.Session.AddMemberAsync(owner.Project.Id, editor.Session.UserId, ProjectMemberRole.Editor);

        var assigned = await owner.Session.PostAsync<TaskItemDto>(ApiRoutes.Tasks(owner.Project.Id), new CreateTaskItemRequest { Title = $"Assigned {owner.Secret}", AssignedUserId = editor.Session.UserId });
        var whileMember = await editor.Session.PutAsync(ApiRoutes.Task(owner.Project.Id, assigned.Id), new UpdateTaskItemRequest { Title = assigned.Title, AssignedUserId = editor.Session.UserId, Status = TaskItemStatus.InProgress });
        await whileMember.ShouldBeAsync(HttpStatusCode.OK);

        var removal = await owner.Session.DeleteAsync(ApiRoutes.ProjectMember(owner.Project.Id, editor.Session.UserId));
        await removal.ShouldBeAsync(HttpStatusCode.NoContent);

        var read = await editor.Session.GetAsync(ApiRoutes.Task(owner.Project.Id, assigned.Id));
        var update = await editor.Session.PutAsync(ApiRoutes.Task(owner.Project.Id, assigned.Id), new UpdateTaskItemRequest { Title = "Hijacked", AssignedUserId = editor.Session.UserId });
        var complete = await editor.Session.PostAsync(ApiRoutes.CompleteTask(owner.Project.Id, assigned.Id));
        var delete = await editor.Session.DeleteAsync(ApiRoutes.Task(owner.Project.Id, assigned.Id));
        var rejoin = await editor.Session.PostAsync(ApiRoutes.ProjectMembers(owner.Project.Id), new AddProjectMemberRequest { UserId = editor.Session.UserId, Role = ProjectMemberRole.Editor });
        var tasks = await editor.Session.GetAsync(ApiRoutes.AllTasks);
        var calendar = await editor.Session.GetAsync($"{ApiRoutes.AllTasks}?from=2000-01-01&to=2100-01-01");

        await read.ShouldBeAsync(HttpStatusCode.Forbidden);
        await update.ShouldBeAsync(HttpStatusCode.Forbidden);
        await complete.ShouldBeAsync(HttpStatusCode.Forbidden);
        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);
        await rejoin.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.DoesNotContain(owner.Secret, await tasks.Content.ReadAsStringAsync());
        Assert.DoesNotContain(owner.Secret, await calendar.Content.ReadAsStringAsync());

        var kept = await owner.Session.GetAsync<TaskItemDto>(ApiRoutes.Task(owner.Project.Id, assigned.Id));
        Assert.Equal(assigned.Title, kept.Title);
        Assert.Equal(TaskItemStatus.InProgress, kept.Status);
    }

    [Fact]
    public async Task RemovedMember_IsNoLongerTheAssigneeOfTheProjectsTasks()
    {
        var owner = await _fixture.CreateOwnerAsync("unassignowner");
        var editor = await _fixture.CreateOwnerAsync("unassigneditor");
        await owner.Session.AddMemberAsync(owner.Project.Id, editor.Session.UserId, ProjectMemberRole.Editor);
        var assigned = await owner.Session.PostAsync<TaskItemDto>(ApiRoutes.Tasks(owner.Project.Id), new CreateTaskItemRequest { Title = "Assigned", AssignedUserId = editor.Session.UserId });

        var removal = await owner.Session.DeleteAsync(ApiRoutes.ProjectMember(owner.Project.Id, editor.Session.UserId));
        await removal.ShouldBeAsync(HttpStatusCode.NoContent);

        var kept = await owner.Session.GetAsync<TaskItemDto>(ApiRoutes.Task(owner.Project.Id, assigned.Id));
        Assert.Null(kept.AssignedUserId);
    }

    [Fact]
    public async Task Administrator_ReachesTheTasksOfEveryUser()
    {
        // The counterpart of the refusals above: the routes exist and answer for a caller who is allowed.
        var admin = await _fixture.Factory.AdminAsync();

        foreach (var owner in _fixture.Owners)
        {
            var task = await admin.GetAsync<TaskItemDto>(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id));
            var content = await admin.GetAsync(ApiRoutes.AttachmentContent(owner.Project.Id, owner.OwnTask.Id, owner.Attachment.Id));

            Assert.Equal(owner.OwnTask.Title, task.Title);
            await content.ShouldBeAsync(HttpStatusCode.OK);
        }
    }

    private IEnumerable<CrossUserContext> Arrangements(bool everyThird)
    {
        foreach (var attacker in _fixture.Owners)
        {
            foreach (var victim in _fixture.Owners.Where(owner => owner != attacker))
            {
                var thirds = _fixture.Owners.Where(owner => owner != attacker && owner != victim);

                foreach (var third in everyThird ? thirds : thirds.Take(1))
                {
                    yield return new CrossUserContext(attacker, victim, third);
                }
            }
        }
    }

    private static async Task AssertUntouchedAsync(TaskOwner owner)
    {
        var session = owner.Session;

        var project = await session.GetAsync<ProjectDto>(ApiRoutes.Project(owner.Project.Id));
        Assert.Equal(owner.Project.Name, project.Name);
        Assert.Equal(owner.Session.UserId, project.OwnerId);
        Assert.True(project.IsActive);

        var tasks = await session.GetAsync<List<TaskItemDto>>(ApiRoutes.Tasks(owner.Project.Id));
        var subTasks = await session.GetAsync<List<TaskItemDto>>(ApiRoutes.SubTasks(owner.Project.Id, owner.ParentTask.Id));
        var taskIds = Flatten(tasks).Concat(subTasks).Select(task => task.Id).Distinct().Order();
        Assert.Equal(owner.TaskIds.Order(), taskIds);
        Assert.Equal(owner.SubTask.Id, Assert.Single(subTasks).Id);

        var ownTask = await session.GetAsync<TaskItemDto>(ApiRoutes.Task(owner.Project.Id, owner.OwnTask.Id));
        Assert.Equal(owner.OwnTask.Title, ownTask.Title);
        Assert.Equal(owner.OwnTask.Description, ownTask.Description);
        Assert.Equal(TaskItemStatus.ToDo, ownTask.Status);
        Assert.Equal(owner.OwnTask.Priority, ownTask.Priority);
        Assert.Equal(owner.Session.UserId, ownTask.AssignedUserId);
        Assert.Null(ownTask.ParentTaskId);
        Assert.Null(ownTask.StartedAt);
        Assert.Null(ownTask.CompletedAt);

        var attachments = await session.GetAsync<List<AttachmentDto>>(ApiRoutes.Attachments(owner.Project.Id, owner.OwnTask.Id));
        Assert.Equal(owner.Attachment.Id, Assert.Single(attachments).Id);

        var content = await session.GetAsync(ApiRoutes.AttachmentContent(owner.Project.Id, owner.OwnTask.Id, owner.Attachment.Id));
        await content.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(owner.AttachmentContent, await content.Content.ReadAsByteArrayAsync());

        var projectComment = Assert.Single(await session.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(owner.Project.Id)));
        Assert.Equal(owner.ProjectComment.Id, projectComment.Id);
        Assert.Equal(owner.ProjectComment.Content, projectComment.Content);
        Assert.Equal(owner.Session.UserId, projectComment.AuthorId);

        var taskComment = Assert.Single(await session.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(owner.Project.Id, owner.OwnTask.Id)));
        Assert.Equal(owner.TaskComment.Id, taskComment.Id);
        Assert.Equal(owner.TaskComment.Content, taskComment.Content);

        Assert.Empty(await session.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(owner.Project.Id, owner.ParentTask.Id)));
        Assert.Empty(await session.GetAsync<List<ProjectTeamDto>>(ApiRoutes.ProjectTeams(owner.Project.Id)));
    }

    // Task lists nest subtasks in some answers and return them flat in others.
    private static IEnumerable<TaskItemDto> Flatten(IEnumerable<TaskItemDto> tasks)
    {
        foreach (var task in tasks)
        {
            yield return task;

            foreach (var subTask in Flatten(task.SubTasks))
            {
                yield return subTask;
            }
        }
    }
}
