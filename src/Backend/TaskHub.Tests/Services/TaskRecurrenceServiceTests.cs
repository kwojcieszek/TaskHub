using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class TaskRecurrenceServiceTests : IDisposable
{
    private static readonly DateTime DueDate = new(2026, 3, 13, 0, 0, 0, DateTimeKind.Utc);

    private readonly TestHost _host = new();
    private readonly ITaskItemService _tasks;

    public TaskRecurrenceServiceTests()
    {
        _tasks = _host.Get<ITaskItemService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Complete_CreatesTheNextOccurrenceThatTakesTheScheduleOver()
    {
        var project = await ArrangeProjectAsync();
        var task = await CreateRepeatingTaskAsync(project.Id, Weekly(RecurrenceBasis.DueDate));

        var completed = await _tasks.CompleteAsync(project.Id, task.Id);
        var next = Assert.Single(await _tasks.GetByProjectAsync(project.Id), t => t.Id != task.Id);

        Assert.Null(completed.Recurrence);
        Assert.Equal(TaskItemStatus.ToDo, next.Status);
        Assert.Equal(task.Title, next.Title);
        Assert.Equal(TaskItemPriority.High, next.Priority);
        Assert.True(next.DueDate > DueDate);
        Assert.Equal(DayOfWeek.Friday, next.DueDate!.Value.DayOfWeek);
        Assert.Equal(RecurrenceBasis.DueDate, next.Recurrence!.Basis);
    }

    [Fact]
    public async Task Complete_FromCompletionDate_CountsFromToday()
    {
        var project = await ArrangeProjectAsync();
        var task = await CreateRepeatingTaskAsync(project.Id, new TaskRecurrenceDto { Interval = 3, Unit = RecurrenceUnit.Day, Basis = RecurrenceBasis.CompletionDate });

        await _tasks.CompleteAsync(project.Id, task.Id);
        var next = Assert.Single(await _tasks.GetByProjectAsync(project.Id), t => t.Id != task.Id);

        Assert.Equal(DateTime.UtcNow.Date.AddDays(3), next.DueDate!.Value.Date);
    }

    [Fact]
    public async Task ReopeningAndCompletingAgain_DoesNotRepeatTwice()
    {
        var project = await ArrangeProjectAsync();
        var task = await CreateRepeatingTaskAsync(project.Id, Weekly(RecurrenceBasis.CompletionDate));
        var completed = await _tasks.CompleteAsync(project.Id, task.Id);

        await _tasks.UpdateAsync(project.Id, task.Id, UpdateRequest(completed, TaskItemStatus.ToDo));
        await _tasks.CompleteAsync(project.Id, task.Id);

        Assert.Equal(2, (await _tasks.GetByProjectAsync(project.Id)).Count);
    }

    [Fact]
    public async Task SettingTheStatusToDone_AlsoRepeats()
    {
        var project = await ArrangeProjectAsync();
        var task = await CreateRepeatingTaskAsync(project.Id, Weekly(RecurrenceBasis.CompletionDate));

        await _tasks.UpdateAsync(project.Id, task.Id, UpdateRequest(task, TaskItemStatus.Done));

        Assert.Equal(2, (await _tasks.GetByProjectAsync(project.Id)).Count);
    }

    [Fact]
    public async Task CompletingTheLastSubtask_RepeatsTheTaskWithFreshSubtasks()
    {
        var project = await ArrangeProjectAsync();
        var task = await CreateRepeatingTaskAsync(project.Id, Weekly(RecurrenceBasis.DueDate));
        var first = await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "first", ParentTaskId = task.Id, DueDate = DueDate.AddDays(-1) });
        var second = await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "second", ParentTaskId = task.Id });

        await _tasks.CompleteAsync(project.Id, first.Id);
        await _tasks.CompleteAsync(project.Id, second.Id);
        var next = Assert.Single(await _tasks.GetByProjectAsync(project.Id), t => t.Id != task.Id);

        Assert.Equal(new[] { "first", "second" }, next.SubTasks.Select(s => s.Title));
        Assert.All(next.SubTasks, s => Assert.Equal(TaskItemStatus.ToDo, s.Status));
        Assert.Equal(next.DueDate!.Value.AddDays(-1), next.SubTasks.First().DueDate);
    }

    [Fact]
    public async Task ASubtask_CannotRepeatOnItsOwn()
    {
        var project = await ArrangeProjectAsync();
        var task = await _tasks.CreateAsync(project.Id, new CreateTaskItemRequest { Title = "parent" });

        await Assert.ThrowsAsync<ValidationFailedException>(() => _tasks.CreateAsync(project.Id, new CreateTaskItemRequest
        {
            Title = "child",
            ParentTaskId = task.Id,
            Recurrence = Weekly(RecurrenceBasis.CompletionDate)
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(TaskRecurrence.MaxInterval + 1)]
    public async Task AnIntervalOutOfRange_IsRefused(int interval)
    {
        var project = await ArrangeProjectAsync();

        await Assert.ThrowsAsync<ValidationFailedException>(() => CreateRepeatingTaskAsync(project.Id, new TaskRecurrenceDto { Interval = interval, Unit = RecurrenceUnit.Day }));
    }

    private static TaskRecurrenceDto Weekly(RecurrenceBasis basis)
    {
        return new TaskRecurrenceDto { Interval = 1, Unit = RecurrenceUnit.Week, Basis = basis };
    }

    private static UpdateTaskItemRequest UpdateRequest(TaskItemDto task, TaskItemStatus status)
    {
        return new UpdateTaskItemRequest
        {
            Title = task.Title,
            DueDate = task.DueDate,
            Status = status,
            Priority = task.Priority,
            Recurrence = task.Recurrence
        };
    }

    private async Task<ProjectDto> ArrangeProjectAsync()
    {
        var owner = await _host.CreateUserAsync("owner");

        return await _host.CreateProjectAsync(owner);
    }

    private Task<TaskItemDto> CreateRepeatingTaskAsync(Guid projectId, TaskRecurrenceDto recurrence)
    {
        return _tasks.CreateAsync(projectId, new CreateTaskItemRequest
        {
            Title = "Weekly report",
            DueDate = DueDate,
            Priority = TaskItemPriority.High,
            Recurrence = recurrence
        });
    }
}
