using System.Net;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Integration;

/// <summary>
/// Tasks and subtasks: created, worked on, completed and deleted through the API.
/// </summary>
public class TaskLifecycleTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public TaskLifecycleTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatedTask_StartsAsToDoWithTheGivenDetails()
    {
        var (user, project) = await CreateProjectAsync();
        var dueDate = new DateTime(2030, 5, 20, 0, 0, 0, DateTimeKind.Utc);

        var task = await user.PostAsync<TaskItemDto>(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest
        {
            Title = "Write the report",
            Description = "Quarterly numbers",
            DueDate = dueDate,
            Priority = TaskItemPriority.High,
            AssignedUserId = user.UserId
        });

        Assert.Equal("Write the report", task.Title);
        Assert.Equal("Quarterly numbers", task.Description);
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
        Assert.Equal(TaskItemPriority.High, task.Priority);
        Assert.Equal(user.UserId, task.AssignedUserId);
        Assert.NotNull(task.AssignedUserDisplayName);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Null(task.StartedAt);
        Assert.Null(task.CompletedAt);

        var reloadedProject = await user.GetAsync<ProjectDto>(ApiRoutes.Project(project.Id));
        Assert.Equal(1, reloadedProject.TaskCount);
    }

    [Fact]
    public async Task Task_IsStartedThenCompleted()
    {
        var (user, project) = await CreateProjectAsync();
        var task = await user.CreateTaskAsync(project.Id);

        var started = await user.PostAsync<TaskItemDto>(ApiRoutes.StartTask(project.Id, task.Id));
        Assert.Equal(TaskItemStatus.InProgress, started.Status);
        Assert.NotNull(started.StartedAt);

        var startAgain = await user.PostAsync(ApiRoutes.StartTask(project.Id, task.Id));
        await startAgain.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var completed = await user.PostAsync<TaskItemDto>(ApiRoutes.CompleteTask(project.Id, task.Id));
        Assert.Equal(TaskItemStatus.Done, completed.Status);
        Assert.NotNull(completed.CompletedAt);

        var completeAgain = await user.PostAsync(ApiRoutes.CompleteTask(project.Id, task.Id));
        var startCompleted = await user.PostAsync(ApiRoutes.StartTask(project.Id, task.Id));
        await completeAgain.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await startCompleted.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatedTask_KeepsTheNewValues()
    {
        var (user, project) = await CreateProjectAsync();
        var task = await user.CreateTaskAsync(project.Id, "Before");

        var updated = await user.PutAsync<TaskItemDto>(ApiRoutes.Task(project.Id, task.Id), new UpdateTaskItemRequest
        {
            Title = "After",
            Description = "Changed",
            Status = TaskItemStatus.InProgress,
            Priority = TaskItemPriority.Low
        });

        Assert.Equal("After", updated.Title);
        Assert.Equal("Changed", updated.Description);
        Assert.Equal(TaskItemStatus.InProgress, updated.Status);
        Assert.Equal(TaskItemPriority.Low, updated.Priority);
        Assert.NotNull(updated.StartedAt);
        Assert.NotNull(updated.UpdatedAt);

        var reopened = await user.PutAsync<TaskItemDto>(ApiRoutes.Task(project.Id, task.Id), new UpdateTaskItemRequest { Title = "After", Status = TaskItemStatus.ToDo });
        Assert.Equal(TaskItemStatus.ToDo, reopened.Status);
        Assert.Null(reopened.StartedAt);
    }

    [Fact]
    public async Task SubTasks_DecideTheStatusOfTheirParent()
    {
        var (user, project) = await CreateProjectAsync();
        var parent = await user.CreateTaskAsync(project.Id, "Parent");
        var first = await user.CreateSubTaskAsync(project.Id, parent.Id, "First");
        var second = await user.CreateSubTaskAsync(project.Id, parent.Id, "Second");

        Assert.Equal(parent.Id, first.ParentTaskId);

        // The parent has no status of its own any more.
        var startParent = await user.PostAsync(ApiRoutes.StartTask(project.Id, parent.Id));
        var completeParent = await user.PostAsync(ApiRoutes.CompleteTask(project.Id, parent.Id));
        await startParent.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await completeParent.ReadErrorsAsync(HttpStatusCode.BadRequest);

        await user.PostAsync<TaskItemDto>(ApiRoutes.StartTask(project.Id, first.Id));
        var inProgress = await user.GetAsync<TaskItemDto>(ApiRoutes.Task(project.Id, parent.Id));
        Assert.Equal(TaskItemStatus.InProgress, inProgress.Status);

        await user.PostAsync<TaskItemDto>(ApiRoutes.CompleteTask(project.Id, first.Id));
        await user.PostAsync<TaskItemDto>(ApiRoutes.CompleteTask(project.Id, second.Id));
        var done = await user.GetAsync<TaskItemDto>(ApiRoutes.Task(project.Id, parent.Id));
        Assert.Equal(TaskItemStatus.Done, done.Status);
        Assert.Equal(2, done.SubTasks.Count);

        var subTasks = await user.GetAsync<List<TaskItemDto>>(ApiRoutes.SubTasks(project.Id, parent.Id));
        Assert.Equal(new[] { first.Id, second.Id }, subTasks.Select(subTask => subTask.Id));

        var reloadedProject = await user.GetAsync<ProjectDto>(ApiRoutes.Project(project.Id));
        Assert.Equal(1, reloadedProject.TaskCount);
        Assert.Equal(2, reloadedProject.SubTaskCount);
    }

    [Fact]
    public async Task SubTasks_CannotBeNested()
    {
        var (user, project) = await CreateProjectAsync();
        var parent = await user.CreateTaskAsync(project.Id, "Parent");
        var subTask = await user.CreateSubTaskAsync(project.Id, parent.Id);
        var other = await user.CreateTaskAsync(project.Id, "Other");

        var nested = await user.PostAsync(ApiRoutes.SubTasks(project.Id, subTask.Id), new CreateTaskItemRequest { Title = "Too deep" });
        var parentBecomesSubTask = await user.PutAsync(ApiRoutes.Task(project.Id, parent.Id), new UpdateTaskItemRequest { Title = "Parent", ParentTaskId = other.Id });
        var ownParent = await user.PutAsync(ApiRoutes.Task(project.Id, other.Id), new UpdateTaskItemRequest { Title = "Other", ParentTaskId = other.Id });
        var missingParent = await user.PostAsync(ApiRoutes.SubTasks(project.Id, Guid.NewGuid()), new CreateTaskItemRequest { Title = "Orphan" });

        await nested.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await parentBecomesSubTask.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await ownParent.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await missingParent.ReadErrorsAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletedTask_TakesItsSubTasksAlong()
    {
        var (user, project) = await CreateProjectAsync();
        var parent = await user.CreateTaskAsync(project.Id, "Parent");
        var subTask = await user.CreateSubTaskAsync(project.Id, parent.Id);
        var other = await user.CreateTaskAsync(project.Id, "Other");

        var delete = await user.DeleteAsync(ApiRoutes.Task(project.Id, parent.Id));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var deletedParent = await user.GetAsync(ApiRoutes.Task(project.Id, parent.Id));
        var deletedSubTask = await user.GetAsync(ApiRoutes.Task(project.Id, subTask.Id));
        var deleteAgain = await user.DeleteAsync(ApiRoutes.Task(project.Id, parent.Id));
        var remaining = await user.GetAsync<List<TaskItemDto>>(ApiRoutes.Tasks(project.Id));

        await deletedParent.ShouldBeAsync(HttpStatusCode.NotFound);
        await deletedSubTask.ShouldBeAsync(HttpStatusCode.NotFound);
        await deleteAgain.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Equal(other.Id, Assert.Single(remaining).Id);
    }

    [Fact]
    public async Task Overview_ShowsTasksOfEveryProjectTheUserBelongsTo()
    {
        var owner = await _factory.CreateUserAsync("owner");
        var member = await _factory.CreateUserAsync("member");
        var own = await member.CreateProjectAsync("Own");
        var shared = await owner.CreateProjectAsync("Shared");
        var hidden = await owner.CreateProjectAsync("Hidden");
        await owner.AddMemberAsync(shared.Id, member.UserId, ProjectMemberRole.Viewer);

        var ownTask = await member.CreateTaskAsync(own.Id, "Own task", new DateTime(2030, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var sharedTask = await owner.CreateTaskAsync(shared.Id, "Shared task", new DateTime(2030, 1, 20, 0, 0, 0, DateTimeKind.Utc));
        var laterTask = await owner.CreateTaskAsync(shared.Id, "Later task", new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        await owner.CreateTaskAsync(hidden.Id, "Hidden task", new DateTime(2030, 1, 15, 0, 0, 0, DateTimeKind.Utc));

        var overview = await member.GetAsync<List<TaskItemDto>>(ApiRoutes.AllTasks);
        var january = await member.GetAsync<List<TaskItemDto>>($"{ApiRoutes.AllTasks}?from=2030-01-01&to=2030-01-31");

        Assert.Equal(new[] { ownTask.Id, sharedTask.Id, laterTask.Id }, overview.Select(task => task.Id));
        Assert.Equal(new[] { ownTask.Id, sharedTask.Id }, january.Select(task => task.Id));
    }

    private async Task<(ApiSession User, ProjectDto Project)> CreateProjectAsync()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();

        return (user, project);
    }
}
