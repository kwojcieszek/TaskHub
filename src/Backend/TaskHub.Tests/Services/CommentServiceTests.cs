using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class CommentServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly ICommentService _comments;

    public CommentServiceTests()
    {
        _comments = _host.Get<ICommentService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task ProjectAndTaskComments_AreKeptApart()
    {
        var (project, _, _, _) = await ArrangeAsync();
        var task = await CreateTaskAsync(project);
        var otherTask = await CreateTaskAsync(project);

        var onProject = await _comments.AddToProjectAsync(project.Id, Content("  On the project  "));
        var onTask = await _comments.AddToTaskAsync(project.Id, task.Id, Content("On the task"));

        var projectComments = await _comments.GetForProjectAsync(project.Id);
        var taskComments = await _comments.GetForTaskAsync(project.Id, task.Id);
        var otherTaskComments = await _comments.GetForTaskAsync(project.Id, otherTask.Id);

        Assert.Equal("On the project", onProject.Content);
        Assert.Null(onProject.TaskItemId);
        Assert.Equal(task.Id, onTask.TaskItemId);
        Assert.Equal(onProject.Id, Assert.Single(projectComments).Id);
        Assert.Equal(onTask.Id, Assert.Single(taskComments).Id);
        Assert.Empty(otherTaskComments);
    }

    [Fact]
    public async Task Comments_AreListedOldestFirst_WithTheAuthorsName()
    {
        var (project, owner, _, viewer) = await ArrangeAsync();
        var first = await _comments.AddToProjectAsync(project.Id, Content("First"));
        _host.SignIn(viewer);
        var second = await _comments.AddToProjectAsync(project.Id, Content("Second"));

        var listed = await _comments.GetForProjectAsync(project.Id);

        Assert.Equal(new[] { first.Id, second.Id }, listed.Select(comment => comment.Id));
        Assert.Equal(owner.DisplayName, listed.First().AuthorDisplayName);
        Assert.Equal(viewer.Id, listed.Last().AuthorId);
        Assert.Equal(DateTimeKind.Utc, listed.First().CreatedAt.Kind);
        Assert.Null(listed.First().UpdatedAt);
    }

    [Fact]
    public async Task EveryMember_MayComment_AndAnOutsiderMayNot()
    {
        var (project, _, editor, viewer) = await ArrangeAsync();
        var task = await CreateTaskAsync(project);
        var outsider = await _host.CreateUserAsync("outsider");

        foreach (var member in new[] { editor, viewer })
        {
            _host.SignIn(member);
            await _comments.AddToProjectAsync(project.Id, Content("Note"));
            await _comments.AddToTaskAsync(project.Id, task.Id, Content("Note"));
        }

        _host.SignIn(outsider);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.GetForProjectAsync(project.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.GetForTaskAsync(project.Id, task.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.AddToProjectAsync(project.Id, Content("Planted")));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.AddToTaskAsync(project.Id, task.Id, Content("Planted")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public async Task BlankComment_IsRejected(string content)
    {
        var (project, _, _, _) = await ArrangeAsync();
        var existing = await _comments.AddToProjectAsync(project.Id, Content("Existing"));

        await Assert.ThrowsAsync<ValidationFailedException>(() => _comments.AddToProjectAsync(project.Id, Content(content)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _comments.UpdateAsync(project.Id, existing.Id, Content(content)));
        Assert.Equal("Existing", Assert.Single(await _comments.GetForProjectAsync(project.Id)).Content);
    }

    [Fact]
    public async Task TaskComment_NeedsATaskOfTheSameProject()
    {
        var (project, owner, _, _) = await ArrangeAsync();
        var otherProject = await _host.CreateProjectAsync(owner, "Other");
        var taskOfOther = await CreateTaskAsync(otherProject);

        await Assert.ThrowsAsync<NotFoundException>(() => _comments.AddToTaskAsync(project.Id, taskOfOther.Id, Content("Misplaced")));
        await Assert.ThrowsAsync<NotFoundException>(() => _comments.GetForTaskAsync(project.Id, taskOfOther.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _comments.AddToTaskAsync(project.Id, Guid.NewGuid(), Content("Nowhere")));
    }

    [Fact]
    public async Task Update_IsForTheAuthorOnly()
    {
        var (project, owner, editor, viewer) = await ArrangeAsync();
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        _host.SignIn(viewer);
        var comment = await _comments.AddToProjectAsync(project.Id, Content("Original"));

        _host.SignIn(owner);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.UpdateAsync(project.Id, comment.Id, Content("By the owner")));
        _host.SignIn(editor);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.UpdateAsync(project.Id, comment.Id, Content("By the editor")));
        _host.SignIn(admin, Roles.Admin);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.UpdateAsync(project.Id, comment.Id, Content("By the administrator")));

        _host.SignIn(viewer);
        var updated = await _comments.UpdateAsync(project.Id, comment.Id, Content("  Corrected "));

        Assert.Equal("Corrected", updated.Content);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(viewer.Id, updated.AuthorId);
    }

    [Fact]
    public async Task Delete_IsForTheAuthorTheOwnerAndAnAdministrator()
    {
        var (project, owner, editor, viewer) = await ArrangeAsync();
        var admin = await _host.CreateUserAsync("admin", Roles.Admin);
        _host.SignIn(viewer);
        var forAuthor = await _comments.AddToProjectAsync(project.Id, Content("For the author"));
        var forOwner = await _comments.AddToProjectAsync(project.Id, Content("For the owner"));
        var forAdmin = await _comments.AddToProjectAsync(project.Id, Content("For the administrator"));

        _host.SignIn(editor);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _comments.DeleteAsync(project.Id, forAuthor.Id));

        _host.SignIn(viewer);
        await _comments.DeleteAsync(project.Id, forAuthor.Id);
        _host.SignIn(owner);
        await _comments.DeleteAsync(project.Id, forOwner.Id);
        _host.SignIn(admin, Roles.Admin);
        await _comments.DeleteAsync(project.Id, forAdmin.Id);

        Assert.Empty(await _comments.GetForProjectAsync(project.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _comments.DeleteAsync(project.Id, forAdmin.Id));
    }

    [Fact]
    public async Task Flags_TellTheCallerWhatTheyMayDo()
    {
        var (project, owner, editor, viewer) = await ArrangeAsync();
        _host.SignIn(viewer);
        await _comments.AddToProjectAsync(project.Id, Content("By the viewer"));

        var asAuthor = Assert.Single(await _comments.GetForProjectAsync(project.Id));
        _host.SignIn(editor);
        var asEditor = Assert.Single(await _comments.GetForProjectAsync(project.Id));
        _host.SignIn(owner);
        var asOwner = Assert.Single(await _comments.GetForProjectAsync(project.Id));

        Assert.True(asAuthor.CanEdit);
        Assert.True(asAuthor.CanDelete);
        Assert.False(asEditor.CanEdit);
        Assert.False(asEditor.CanDelete);
        Assert.False(asOwner.CanEdit);
        Assert.True(asOwner.CanDelete);
    }

    [Fact]
    public async Task Comment_IsNotReachableThroughAnotherProject()
    {
        var (project, owner, _, _) = await ArrangeAsync();
        var otherProject = await _host.CreateProjectAsync(owner, "Other");
        var comment = await _comments.AddToProjectAsync(project.Id, Content("Here"));

        await Assert.ThrowsAsync<NotFoundException>(() => _comments.UpdateAsync(otherProject.Id, comment.Id, Content("There")));
        await Assert.ThrowsAsync<NotFoundException>(() => _comments.DeleteAsync(otherProject.Id, comment.Id));
        Assert.Equal("Here", Assert.Single(await _comments.GetForProjectAsync(project.Id)).Content);
    }

    [Fact]
    public async Task DeletingATask_RemovesTheCommentsOfItAndItsSubTasks()
    {
        var (project, _, _, _) = await ArrangeAsync();
        var tasks = _host.Get<ITaskItemService>();
        var parent = await CreateTaskAsync(project);
        var subTask = await tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Subtask", ParentTaskId = parent.Id });
        var other = await CreateTaskAsync(project);
        await _comments.AddToTaskAsync(project.Id, parent.Id, Content("On the parent"));
        await _comments.AddToTaskAsync(project.Id, subTask.Id, Content("On the subtask"));
        await _comments.AddToTaskAsync(project.Id, other.Id, Content("On another task"));
        await _comments.AddToProjectAsync(project.Id, Content("On the project"));

        await tasks.DeleteAsync(project.Id, parent.Id);

        Assert.Single(await _comments.GetForTaskAsync(project.Id, other.Id));
        Assert.Single(await _comments.GetForProjectAsync(project.Id));
        Assert.Equal(2, _host.Db.Comments.Count());
    }

    [Fact]
    public async Task DeletingAProject_RemovesAllItsComments()
    {
        var (project, _, _, _) = await ArrangeAsync();
        var tasks = _host.Get<ITaskItemService>();
        var parent = await CreateTaskAsync(project);
        var subTask = await tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Subtask", ParentTaskId = parent.Id });
        await _comments.AddToProjectAsync(project.Id, Content("On the project"));
        await _comments.AddToTaskAsync(project.Id, subTask.Id, Content("On the subtask"));

        await _host.Get<IProjectService>().DeleteAsync(project.Id);

        Assert.Equal(0, _host.Db.Comments.Count());
    }

    private async Task<(ProjectDto Project, User Owner, User Editor, User Viewer)> ArrangeAsync()
    {
        var owner = await _host.CreateUserAsync("owner");
        var editor = await _host.CreateUserAsync("editor");
        var viewer = await _host.CreateUserAsync("viewer");
        var project = await _host.CreateProjectAsync(owner);
        await _host.AddMemberAsync(project, owner, editor, ProjectMemberRole.Editor);
        await _host.AddMemberAsync(project, owner, viewer, ProjectMemberRole.Viewer);

        return (project, owner, editor, viewer);
    }

    private Task<TaskItemDto> CreateTaskAsync(ProjectDto project)
    {
        return _host.Get<ITaskItemService>().CreateAsync(project.Id, new CreateTaskItemRequest { Title = "Task" });
    }

    private static SaveCommentRequest Content(string content)
    {
        return new SaveCommentRequest { Content = content };
    }
}
