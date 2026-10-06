using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/calendar")]
public class CalendarController : ApiControllerBase
{
    private readonly ITaskItemService _taskItemService;

    public CalendarController(ITaskItemService taskItemService)
    {
        _taskItemService = taskItemService;
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<IReadOnlyCollection<TaskItemDto>>> GetTasks(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        var tasks = await _taskItemService.GetCalendarAsync(from, to, cancellationToken);
        return Ok(tasks);
    }

    [HttpGet("my-tasks")]
    public async Task<ActionResult<IReadOnlyCollection<TaskItemDto>>> GetMyTasks(CancellationToken cancellationToken)
    {
        var tasks = await _taskItemService.GetMyTasksAsync(cancellationToken);
        return Ok(tasks);
    }
}
