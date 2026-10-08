using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class TaskItemServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly ITaskItemService _tasks;

    public TaskItemServiceTests()
    {
        _tasks = _host.Get<ITaskItemService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Start_MovesTaskToInProgressAndRecordsStartTime()
    {
        var (project, _) = await ArrangeProjectAsync();
        var task = await CreateTaskAsync(project.Id, "task");

        var started = await _tasks.StartAsync(project.Id, task.Id);

        Assert.Equal(TaskItemStatus.InProgress, started.Status);
        Assert.NotNull(started.StartedAt);
        Assert.Null(started.CompletedAt);
    }

    [Fact]
    public async Task Start_FailsWhenTaskWasAlreadyStarted()
    {
        var (project, _) = await ArrangeProjectAsync();
        var task = await CreateTaskAsync(project.Id, "task");
        await _tasks.StartAsync(project.Id, task.Id);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _tasks.StartAsync(project.Id, task.Id));
    }

    [Fact]
    public async Task Complete_AfterStart_KeepsStartTimeAndRecordsCompletion()
    {
        var (project, _) = await ArrangeProjectAsync();
        var task = await CreateTaskAsync(project.Id, "task");
        await _tasks.StartAsync(project.Id, task.Id);

        var completed = await _tasks.CompleteAsync(project.Id, task.Id);

        Assert.Equal(TaskItemStatus.Done, completed.Status);
        Assert.NotNull(completed.StartedAt);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task Complete_WithoutStart_IsAllowedAndLeavesStartTimeEmpty()
    {
        var (project, _) = await ArrangeProjectAsync();
        var task = await CreateTaskAsync(project.Id, "task");

        var completed = await _tasks.CompleteAsync(project.Id, task.Id);

        Assert.Equal(TaskItemStatus.Done, completed.Status);
        Assert.Null(completed.StartedAt);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task Complete_FailsWhenTaskIsAlreadyDone()
    {
        var (project, _) = await ArrangeProjectAsync();
        var task = await CreateTaskAsync(project.Id, "task");
        await _tasks.CompleteAsync(project.Id, task.Id);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _tasks.CompleteAsync(project.Id, task.Id));
    }

    [Fact]
    public async Task Update_BackToToDo_ClearsStartAndCompletionTimes()
    {
        var (project, _) = await ArrangeProjectAsync();
        var task = await CreateTaskAsync(project.Id, "task");
        await _tasks.StartAsync(project.Id, task.Id);
        await _tasks.CompleteAsync(project.Id, task.Id);

        var reopened = await UpdateStatusAsync(project.Id, task, TaskItemStatus.ToDo);

        Assert.Equal(TaskItemStatus.ToDo, reopened.Status);
        Assert.Null(reopened.StartedAt);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public async Task TaskWithSubTasks_CannotBeStartedOrCompletedDirectly()
    {
        var (project, _) = await ArrangeProjectAsync();
        var parent = await CreateTaskAsync(project.Id, "parent");
        await CreateTaskAsync(project.Id, "child", parent.Id);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _tasks.StartAsync(project.Id, parent.Id));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _tasks.CompleteAsync(project.Id, parent.Id));
    }

    [Fact]
    public async Task TaskWithSubTasks_IgnoresStatusSentInUpdate()
    {
        var (project, _) = await ArrangeProjectAsync();
        var parent = await CreateTaskAsync(project.Id, "parent");
        await CreateTaskAsync(project.Id, "child", parent.Id);

        var updated = await UpdateStatusAsync(project.Id, parent, TaskItemStatus.Done);

        Assert.Equal(TaskItemStatus.ToDo, updated.Status);
    }

    [Fact]
    public async Task ParentStatus_FollowsSubTasksFromStartToCompletion()
    {
        var (project, _) = await ArrangeProjectAsync();
        var parent = await CreateTaskAsync(project.Id, "parent");
        var first = await CreateTaskAsync(project.Id, "first", parent.Id);
        var second = await CreateTaskAsync(project.Id, "second", parent.Id);

        await _tasks.StartAsync(project.Id, first.Id);
        var whileRunning = await _tasks.GetByIdAsync(project.Id, parent.Id);

        await _tasks.CompleteAsync(project.Id, first.Id);
        var halfDone = await _tasks.GetByIdAsync(project.Id, parent.Id);

        await _tasks.CompleteAsync(project.Id, second.Id);
        var allDone = await _tasks.GetByIdAsync(project.Id, parent.Id);

        Assert.Equal(TaskItemStatus.InProgress, whileRunning.Status);
        Assert.NotNull(whileRunning.StartedAt);
        Assert.Equal(TaskItemStatus.InProgress, halfDone.Status);
        Assert.Null(halfDone.CompletedAt);
        Assert.Equal(TaskItemStatus.Done, allDone.Status);
        Assert.NotNull(allDone.CompletedAt);
    }

    [Fact]
    public async Task ParentStatus_ReturnsToInProgressWhenSubTaskIsReopened()
    {
        var (project, _) = await ArrangeProjectAsync();
        var parent = await CreateTaskAsync(project.Id, "parent");
        var first = await CreateTaskAsync(project.Id, "first", parent.Id);
        var second = await CreateTaskAsync(project.Id, "second", parent.Id);
        await _tasks.CompleteAsync(project.Id, first.Id);
        await _tasks.CompleteAsync(project.Id, second.Id);

        await UpdateStatusAsync(project.Id, second, TaskItemStatus.ToDo);
        var reopened = await _tasks.GetByIdAsync(project.Id, parent.Id);

        Assert.Equal(TaskItemStatus.InProgress, reopened.Status);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public async Task ParentStatus_IgnoresCancelledSubTasks()
    {
        var (project, _) = await ArrangeProjectAsync();
        var parent = await CreateTaskAsync(project.Id, "parent");
        var first = await CreateTaskAsync(project.Id, "first", parent.Id);
        var second = await CreateTaskAsync(project.Id, "second", parent.Id);

        await UpdateStatusAsync(project.Id, second, TaskItemStatus.Cancelled);
        await _tasks.CompleteAsync(project.Id, first.Id);
        var result = await _tasks.GetByIdAsync(project.Id, parent.Id);

        Assert.Equal(TaskItemStatus.Done, result.Status);
    }

    [Fact]
    public async Task ParentStatus_IsRecalculatedWhenSubTaskIsDeleted()
    {
        var (project, _) = await ArrangeProjectAsync();
        var parent = await CreateTaskAsync(project.Id, "parent");
        var first = await CreateTaskAsync(project.Id, "first", parent.Id);
        var second = await CreateTaskAsync(project.Id, "second", parent.Id);
        await _tasks.CompleteAsync(project.Id, first.Id);

        await _tasks.DeleteAsync(project.Id, second.Id);
        var result = await _tasks.GetByIdAsync(project.Id, parent.Id);

        Assert.Equal(TaskItemStatus.Done, result.Status);
    }

    [Fact]
    public async Task Create_RejectsSubTaskOfSubTask()
    {
        var (project, _) = await ArrangeProjectAsync();
        var parent = await CreateTaskAsync(project.Id, "parent");
        var child = await CreateTaskAsync(project.Id, "child", parent.Id);

        await Assert.ThrowsAsync<ValidationFailedException>(() => CreateTaskAsync(project.Id, "grandchild", child.Id));
    }

    [Fact]
    public async Task Create_RejectsParentFromAnotherProject()
    {
        var (project, owner) = await ArrangeProjectAsync();
        var otherProject = await _host.CreateProjectAsync(owner, "other");
        var foreignParent = await CreateTaskAsync(otherProject.Id, "foreign");

        await Assert.ThrowsAsync<NotFoundException>(() => CreateTaskAsync(project.Id, "child", foreignParent.Id));
    }

    [Fact]
    public async Task GetMyTasks_ReturnsOnlyTasksOfProjectsTheUserBelongsTo()
    {
        var (project, _) = await ArrangeProjectAsync();
        await CreateTaskAsync(project.Id, "mine");
        var stranger = await _host.CreateUserAsync("stranger");
        var strangersProject = await _host.CreateProjectAsync(stranger, "theirs");
        await CreateTaskAsync(strangersProject.Id, "theirs");

        var tasks = await _tasks.GetMyTasksAsync();

        Assert.Equal("theirs", Assert.Single(tasks).Title);
    }

    [Fact]
    public async Task GetMyTasks_LeavesOutTasksOfDeactivatedProjects()
    {
        var (project, _) = await ArrangeProjectAsync();
        await CreateTaskAsync(project.Id, "task");

        await _host.Get<IProjectService>().UpdateAsync(project.Id, new UpdateProjectRequest { Name = project.Name, IsActive = false });
        var tasks = await _tasks.GetMyTasksAsync();
        var insideProject = await _tasks.GetByProjectAsync(project.Id);

        Assert.Empty(tasks);
        Assert.Single(insideProject);
    }

    private async Task<(ProjectDto Project, User Owner)> ArrangeProjectAsync()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner);

        return (project, owner);
    }

    private Task<TaskItemDto> CreateTaskAsync(Guid projectId, string title, Guid? parentTaskId = null)
    {
        return _tasks.CreateAsync(projectId, new CreateTaskItemRequest { Title = title, ParentTaskId = parentTaskId });
    }

    private Task<TaskItemDto> UpdateStatusAsync(Guid projectId, TaskItemDto task, TaskItemStatus status)
    {
        return _tasks.UpdateAsync(projectId, task.Id, new UpdateTaskItemRequest
        {
            ParentTaskId = task.ParentTaskId,
            Title = task.Title,
            Status = status,
            Priority = task.Priority
        });
    }
}
