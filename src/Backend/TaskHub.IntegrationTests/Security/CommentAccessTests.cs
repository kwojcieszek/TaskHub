using System.Net;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Comments on a project and on its tasks: read and written by everyone who can open the project, changed only
/// by their author, removed by the author or whoever manages the project, and closed to everyone else.
/// </summary>
public class CommentAccessTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public CommentAccessTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryMember_ReadsAndWritesComments()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var cast = scenario.Cast;
        var project = scenario.Project.Id;
        var task = scenario.LeafTask.Id;

        foreach (var member in new[] { cast.Owner, cast.Editor, cast.Viewer, cast.Admin })
        {
            var onProject = await member.CommentOnProjectAsync(project, $"Project note by {member.UserName}");
            var onTask = await member.CommentOnTaskAsync(project, task, $"Task note by {member.UserName}");

            Assert.Equal(member.UserId, onProject.AuthorId);
            Assert.Null(onProject.TaskItemId);
            Assert.Equal(task, onTask.TaskItemId);
            Assert.True(onProject.CanEdit);
            Assert.True(onProject.CanDelete);
        }

        var projectComments = await cast.Viewer.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project));
        var taskComments = await cast.Viewer.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(project, task));
        var otherTaskComments = await cast.Viewer.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(project, scenario.ParentTask.Id));

        Assert.Equal(4, projectComments.Count);
        Assert.Equal(4, taskComments.Count);
        Assert.All(projectComments, comment => Assert.Null(comment.TaskItemId));
        Assert.All(taskComments, comment => Assert.Equal(task, comment.TaskItemId));
        Assert.Empty(otherTaskComments);
        Assert.Equal(projectComments.OrderBy(comment => comment.CreatedAt).Select(comment => comment.Id), projectComments.Select(comment => comment.Id));
    }

    [Fact]
    public async Task Outsider_CannotReadOrWriteComments()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var project = scenario.Project.Id;
        var task = scenario.LeafTask.Id;
        var comment = await scenario.Cast.Owner.CommentOnProjectAsync(project, "Private note");
        var request = new SaveCommentRequest { Content = "Planted" };

        foreach (var (caller, expected) in new[] { (scenario.Cast.Stranger, HttpStatusCode.Forbidden), (scenario.Cast.Anonymous, HttpStatusCode.Unauthorized) })
        {
            var readProject = await caller.GetAsync(ApiRoutes.ProjectComments(project));
            var readTask = await caller.GetAsync(ApiRoutes.TaskComments(project, task));
            var addToProject = await caller.PostAsync(ApiRoutes.ProjectComments(project), request);
            var addToTask = await caller.PostAsync(ApiRoutes.TaskComments(project, task), request);
            var update = await caller.PutAsync(ApiRoutes.Comment(project, comment.Id), request);
            var delete = await caller.DeleteAsync(ApiRoutes.Comment(project, comment.Id));

            await readProject.ShouldBeAsync(expected);
            await readTask.ShouldBeAsync(expected);
            await addToProject.ShouldBeAsync(expected);
            await addToTask.ShouldBeAsync(expected);
            await update.ShouldBeAsync(expected);
            await delete.ShouldBeAsync(expected);
            Assert.DoesNotContain("Private note", await readProject.Content.ReadAsStringAsync());
        }

        var comments = await scenario.Cast.Owner.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project));
        Assert.Equal("Private note", Assert.Single(comments).Content);
    }

    [Fact]
    public async Task Comment_IsChangedOnlyByItsAuthor()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var cast = scenario.Cast;
        var project = scenario.Project.Id;
        var comment = await cast.Viewer.CommentOnProjectAsync(project, "Original");
        var request = new SaveCommentRequest { Content = "Rewritten" };

        var byOwner = await cast.Owner.PutAsync(ApiRoutes.Comment(project, comment.Id), request);
        var byEditor = await cast.Editor.PutAsync(ApiRoutes.Comment(project, comment.Id), request);
        var byAdmin = await cast.Admin.PutAsync(ApiRoutes.Comment(project, comment.Id), request);
        var unchanged = Assert.Single(await cast.Owner.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project)));
        var byAuthor = await cast.Viewer.PutAsync<CommentDto>(ApiRoutes.Comment(project, comment.Id), new SaveCommentRequest { Content = "  Corrected  " });

        await byOwner.ShouldBeAsync(HttpStatusCode.Forbidden);
        await byEditor.ShouldBeAsync(HttpStatusCode.Forbidden);
        await byAdmin.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.Equal("Original", unchanged.Content);
        Assert.Null(unchanged.UpdatedAt);
        Assert.False(unchanged.CanEdit);
        Assert.True(unchanged.CanDelete);
        Assert.Equal("Corrected", byAuthor.Content);
        Assert.NotNull(byAuthor.UpdatedAt);
        Assert.Equal(cast.Viewer.UserId, byAuthor.AuthorId);
    }

    [Fact]
    public async Task Comment_IsDeletedByItsAuthorOrWhoeverManagesTheProject()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var cast = scenario.Cast;
        var project = scenario.Project.Id;
        var byViewer = await cast.Viewer.CommentOnProjectAsync(project, "By the viewer");
        var forOwner = await cast.Viewer.CommentOnProjectAsync(project, "For the owner to remove");
        var forAdmin = await cast.Editor.CommentOnProjectAsync(project, "For the administrator to remove");
        var byOwner = await cast.Owner.CommentOnProjectAsync(project, "By the owner");

        var editorSees = (await cast.Editor.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project))).Single(comment => comment.Id == byViewer.Id);
        var editorDeletes = await cast.Editor.DeleteAsync(ApiRoutes.Comment(project, byViewer.Id));
        var viewerDeletesOwners = await cast.Viewer.DeleteAsync(ApiRoutes.Comment(project, byOwner.Id));
        var authorDeletes = await cast.Viewer.DeleteAsync(ApiRoutes.Comment(project, byViewer.Id));
        var ownerDeletes = await cast.Owner.DeleteAsync(ApiRoutes.Comment(project, forOwner.Id));
        var adminDeletes = await cast.Admin.DeleteAsync(ApiRoutes.Comment(project, forAdmin.Id));
        var deleteAgain = await cast.Owner.DeleteAsync(ApiRoutes.Comment(project, forOwner.Id));

        Assert.False(editorSees.CanDelete);
        await editorDeletes.ShouldBeAsync(HttpStatusCode.Forbidden);
        await viewerDeletesOwners.ShouldBeAsync(HttpStatusCode.Forbidden);
        await authorDeletes.ShouldBeAsync(HttpStatusCode.NoContent);
        await ownerDeletes.ShouldBeAsync(HttpStatusCode.NoContent);
        await adminDeletes.ShouldBeAsync(HttpStatusCode.NoContent);
        await deleteAgain.ShouldBeAsync(HttpStatusCode.NotFound);

        var left = await cast.Owner.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project));
        Assert.Equal(byOwner.Id, Assert.Single(left).Id);
    }

    [Fact]
    public async Task Comment_OfAnotherProject_CannotBeReachedThroughOwnProject()
    {
        var mine = await ProjectScenario.CreateAsync(_factory);
        var attacker = mine.Cast.Stranger;
        var ownProject = await attacker.CreateProjectAsync("Attacker project");
        var ownTask = await attacker.CreateTaskAsync(ownProject.Id);
        var foreignComment = await mine.Cast.Owner.CommentOnTaskAsync(mine.Project.Id, mine.LeafTask.Id, "Foreign note");
        var request = new SaveCommentRequest { Content = "Hijacked" };

        var update = await attacker.PutAsync(ApiRoutes.Comment(ownProject.Id, foreignComment.Id), request);
        var delete = await attacker.DeleteAsync(ApiRoutes.Comment(ownProject.Id, foreignComment.Id));
        var readForeignTask = await attacker.GetAsync(ApiRoutes.TaskComments(ownProject.Id, mine.LeafTask.Id));
        var addToForeignTask = await attacker.PostAsync(ApiRoutes.TaskComments(ownProject.Id, mine.LeafTask.Id), request);
        var ownTaskComments = await attacker.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(ownProject.Id, ownTask.Id));
        var ownProjectComments = await attacker.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(ownProject.Id));

        await update.ShouldBeAsync(HttpStatusCode.NotFound);
        await delete.ShouldBeAsync(HttpStatusCode.NotFound);
        await readForeignTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await addToForeignTask.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Empty(ownTaskComments);
        Assert.Empty(ownProjectComments);

        var untouched = Assert.Single(await mine.Cast.Owner.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(mine.Project.Id, mine.LeafTask.Id)));
        Assert.Equal("Foreign note", untouched.Content);
    }

    [Fact]
    public async Task Author_CannotBeForgedThroughTheRequestBody()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var cast = scenario.Cast;
        var project = scenario.Project.Id;

        var created = await cast.Viewer.PostAsync<CommentDto>(ApiRoutes.ProjectComments(project), new
        {
            content = "Framed",
            authorId = cast.Owner.UserId,
            projectId = Guid.NewGuid(),
            taskItemId = scenario.LeafTask.Id,
            id = Guid.NewGuid(),
            createdAt = "2000-01-01T00:00:00Z"
        });

        Assert.Equal(cast.Viewer.UserId, created.AuthorId);
        Assert.Equal(project, created.ProjectId);
        Assert.Null(created.TaskItemId);
        Assert.True(created.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task EmptyComment_IsRefused(string content)
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var owner = scenario.Cast.Owner;
        var existing = await owner.CommentOnProjectAsync(scenario.Project.Id, "Existing");

        var add = await owner.PostAsync(ApiRoutes.ProjectComments(scenario.Project.Id), new SaveCommentRequest { Content = content });
        var update = await owner.PutAsync(ApiRoutes.Comment(scenario.Project.Id, existing.Id), new SaveCommentRequest { Content = content });
        var tooLong = await owner.PostAsync(ApiRoutes.ProjectComments(scenario.Project.Id), new SaveCommentRequest { Content = new string('a', SaveCommentRequest.MaxContentLength + 1) });
        var longest = await owner.PostAsync(ApiRoutes.ProjectComments(scenario.Project.Id), new SaveCommentRequest { Content = new string('a', SaveCommentRequest.MaxContentLength) });

        await add.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await update.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await tooLong.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await longest.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Comment_IsStoredAndReturnedAsPlainText()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var owner = scenario.Cast.Owner;
        const string hostile = "<script>alert(1)</script> <img src=x onerror=alert(1)> '; DROP TABLE Comments; -- {{constructor}}";

        var created = await owner.CommentOnProjectAsync(scenario.Project.Id, hostile);
        var response = await owner.GetAsync(ApiRoutes.ProjectComments(scenario.Project.Id));
        var listed = Assert.Single(await response.ReadAsync<List<CommentDto>>());

        Assert.Equal(hostile, created.Content);
        Assert.Equal(hostile, listed.Content);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DeletingATask_RemovesItsComments()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var owner = scenario.Cast.Owner;
        var project = scenario.Project.Id;
        await owner.CommentOnTaskAsync(project, scenario.ParentTask.Id, "On the parent");
        await owner.CommentOnTaskAsync(project, scenario.SubTask.Id, "On the subtask");
        await owner.CommentOnTaskAsync(project, scenario.LeafTask.Id, "On another task");
        await owner.CommentOnProjectAsync(project, "On the project");

        var deleteParent = await owner.DeleteAsync(ApiRoutes.Task(project, scenario.ParentTask.Id));
        var commentsOfDeleted = await owner.GetAsync(ApiRoutes.TaskComments(project, scenario.ParentTask.Id));
        var otherTask = await owner.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(project, scenario.LeafTask.Id));
        var projectComments = await owner.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project));

        await deleteParent.ShouldBeAsync(HttpStatusCode.NoContent);
        await commentsOfDeleted.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Single(otherTask);
        Assert.Single(projectComments);
    }

    [Fact]
    public async Task DeletingAProject_WithComments_Succeeds()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var owner = scenario.Cast.Owner;
        var project = scenario.Project.Id;
        await owner.CommentOnProjectAsync(project, "On the project");
        await owner.CommentOnTaskAsync(project, scenario.SubTask.Id, "On the subtask");
        await scenario.Cast.Viewer.CommentOnTaskAsync(project, scenario.LeafTask.Id, "On a task");

        var delete = await owner.DeleteAsync(ApiRoutes.Project(project));
        var after = await owner.GetAsync(ApiRoutes.ProjectComments(project));

        await delete.ShouldBeAsync(HttpStatusCode.NoContent);
        await after.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeletingTheAuthor_KeepsTheComment()
    {
        var admin = await _factory.AdminAsync();
        var owner = await _factory.CreateUserAsync("commentowner");
        var author = await _factory.CreateUserAsync("commentauthor");
        var project = await owner.CreateProjectAsync("Outlives its author");
        await owner.AddMemberAsync(project.Id, author.UserId, Domain.Enums.ProjectMemberRole.Viewer);
        var comment = await author.CommentOnProjectAsync(project.Id, "Left behind");

        var delete = await admin.DeleteAsync(ApiRoutes.AdminUser(author.UserId));
        var kept = Assert.Single(await owner.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project.Id)));

        await delete.ShouldBeAsync(HttpStatusCode.NoContent);
        Assert.Equal(comment.Id, kept.Id);
        Assert.Equal("Left behind", kept.Content);
        Assert.Null(kept.AuthorId);
        Assert.Null(kept.AuthorDisplayName);
    }

    [Fact]
    public async Task FormerMember_LosesTheirCommentsRights()
    {
        var owner = await _factory.CreateUserAsync("commentowner");
        var member = await _factory.CreateUserAsync("commentformer");
        var project = await owner.CreateProjectAsync("Left by a member");
        await owner.AddMemberAsync(project.Id, member.UserId, Domain.Enums.ProjectMemberRole.Editor);
        var comment = await member.CommentOnProjectAsync(project.Id, "Before leaving");

        var removal = await owner.DeleteAsync(ApiRoutes.ProjectMember(project.Id, member.UserId));
        var read = await member.GetAsync(ApiRoutes.ProjectComments(project.Id));
        var update = await member.PutAsync(ApiRoutes.Comment(project.Id, comment.Id), new SaveCommentRequest { Content = "After leaving" });
        var delete = await member.DeleteAsync(ApiRoutes.Comment(project.Id, comment.Id));

        await removal.ShouldBeAsync(HttpStatusCode.NoContent);
        await read.ShouldBeAsync(HttpStatusCode.Forbidden);
        await update.ShouldBeAsync(HttpStatusCode.Forbidden);
        await delete.ShouldBeAsync(HttpStatusCode.Forbidden);

        var kept = Assert.Single(await owner.GetAsync<List<CommentDto>>(ApiRoutes.ProjectComments(project.Id)));
        Assert.Equal("Before leaving", kept.Content);
    }

    [Fact]
    public async Task TaskComment_CannotBeAddedToATaskOfAnotherProjectOfTheSameOwner()
    {
        var owner = await _factory.CreateUserAsync("commentowner");
        var first = await owner.CreateProjectAsync("First");
        var second = await owner.CreateProjectAsync("Second");
        var taskOfSecond = await owner.CreateTaskAsync(second.Id);

        var add = await owner.PostAsync(ApiRoutes.TaskComments(first.Id, taskOfSecond.Id), new SaveCommentRequest { Content = "Misplaced" });
        var read = await owner.GetAsync(ApiRoutes.TaskComments(first.Id, taskOfSecond.Id));
        var task = await owner.GetAsync<TaskItemDto>(ApiRoutes.Task(second.Id, taskOfSecond.Id));

        await add.ShouldBeAsync(HttpStatusCode.NotFound);
        await read.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Empty(await owner.GetAsync<List<CommentDto>>(ApiRoutes.TaskComments(second.Id, task.Id)));
    }
}
