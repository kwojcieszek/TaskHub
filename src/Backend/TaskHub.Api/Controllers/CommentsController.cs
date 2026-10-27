using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/projects/{projectId:guid}")]
public class CommentsController : ApiControllerBase
{
    private readonly ICommentService _commentService;

    public CommentsController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet("comments")]
    public async Task<ActionResult<IReadOnlyCollection<CommentDto>>> GetForProject(Guid projectId, CancellationToken cancellationToken)
    {
        var comments = await _commentService.GetForProjectAsync(projectId, cancellationToken);
        return Ok(comments);
    }

    [HttpPost("comments")]
    public async Task<ActionResult<CommentDto>> AddToProject(Guid projectId, SaveCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await _commentService.AddToProjectAsync(projectId, request, cancellationToken);
        return Ok(comment);
    }

    [HttpGet("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyCollection<CommentDto>>> GetForTask(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var comments = await _commentService.GetForTaskAsync(projectId, taskId, cancellationToken);
        return Ok(comments);
    }

    [HttpPost("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<CommentDto>> AddToTask(Guid projectId, Guid taskId, SaveCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await _commentService.AddToTaskAsync(projectId, taskId, request, cancellationToken);
        return Ok(comment);
    }

    // Changing and deleting work the same for a comment on the project and for one on a task.
    [HttpPut("comments/{commentId:guid}")]
    public async Task<ActionResult<CommentDto>> Update(Guid projectId, Guid commentId, SaveCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await _commentService.UpdateAsync(projectId, commentId, request, cancellationToken);
        return Ok(comment);
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid commentId, CancellationToken cancellationToken)
    {
        await _commentService.DeleteAsync(projectId, commentId, cancellationToken);
        return NoContent();
    }
}
