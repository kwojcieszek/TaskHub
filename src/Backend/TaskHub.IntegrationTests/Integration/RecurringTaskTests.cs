using System.Net;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Integration;

/// <summary>
/// A repeating task creates its next occurrence when it is completed through the API.
/// </summary>
public class RecurringTaskTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public RecurringTaskTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompletingARepeatingTask_AddsTheNextOccurrence()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();
        var dueDate = new DateTime(2030, 5, 20, 0, 0, 0, DateTimeKind.Utc);

        var task = await user.PostAsync<TaskItemDto>(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest
        {
            Title = "Pay the rent",
            DueDate = dueDate,
            Recurrence = new TaskRecurrenceDto { Interval = 1, Unit = RecurrenceUnit.Month, Basis = RecurrenceBasis.DueDate }
        });
        await (await user.PostAsync(ApiRoutes.CompleteTask(project.Id, task.Id))).ShouldBeAsync(HttpStatusCode.OK);

        var tasks = await user.GetAsync<List<TaskItemDto>>(ApiRoutes.Tasks(project.Id));
        var next = Assert.Single(tasks, t => t.Id != task.Id);

        Assert.Equal(RecurrenceUnit.Month, task.Recurrence!.Unit);
        Assert.Equal(dueDate.AddMonths(1), next.DueDate);
        Assert.Equal(TaskItemStatus.ToDo, next.Status);
        Assert.Equal(RecurrenceBasis.DueDate, next.Recurrence!.Basis);
        Assert.Null(tasks.Single(t => t.Id == task.Id).Recurrence);
    }

    [Fact]
    public async Task AnInvalidInterval_IsRejected()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();

        var response = await user.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest
        {
            Title = "Never",
            Recurrence = new TaskRecurrenceDto { Interval = 0, Unit = RecurrenceUnit.Day }
        });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }
}
