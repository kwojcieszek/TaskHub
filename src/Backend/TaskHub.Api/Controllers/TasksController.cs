using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects/{projectId:guid}/tasks")]
public class TasksController : ApiControllerBase
{
    private readonly ITaskItemService _taskItemService;

    public TasksController(ITaskItemService taskItemService)
    {
        _taskItemService = taskItemService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TaskItemDto>>> GetAll(Guid projectId, CancellationToken cancellationToken)
    {
        var tasks = await _taskItemService.GetByProjectAsync(projectId, cancellationToken);
        return Ok(tasks);
    }

    [HttpGet("{taskId:guid}/subtasks")]
    public async Task<ActionResult<IReadOnlyCollection<TaskItemDto>>> GetSubTasks(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var subTasks = await _taskItemService.GetSubTasksAsync(projectId, taskId, cancellationToken);
        return Ok(subTasks);
    }

    [HttpPost("{taskId:guid}/subtasks")]
    public async Task<ActionResult<TaskItemDto>> CreateSubTask(Guid projectId, Guid taskId, CreateTaskItemRequest request, CancellationToken cancellationToken)
    {
        request.ParentTaskId = taskId;
        var subTask = await _taskItemService.CreateAsync(projectId, request, cancellationToken);
        return Ok(subTask);
    }

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<TaskItemDto>> GetById(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _taskItemService.GetByIdAsync(projectId, taskId, cancellationToken);
        return Ok(task);
    }

    [HttpPost]
    public async Task<ActionResult<TaskItemDto>> Create(Guid projectId, CreateTaskItemRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskItemService.CreateAsync(projectId, request, cancellationToken);
        return Ok(task);
    }

    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<TaskItemDto>> Update(Guid projectId, Guid taskId, UpdateTaskItemRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskItemService.UpdateAsync(projectId, taskId, request, cancellationToken);
        return Ok(task);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        await _taskItemService.DeleteAsync(projectId, taskId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{taskId:guid}/start")]
    public async Task<ActionResult<TaskItemDto>> Start(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _taskItemService.StartAsync(projectId, taskId, cancellationToken);
        return Ok(task);
    }

    [HttpPost("{taskId:guid}/complete")]
    public async Task<ActionResult<TaskItemDto>> Complete(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _taskItemService.CompleteAsync(projectId, taskId, cancellationToken);
        return Ok(task);
    }
}
