using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects/{projectId:guid}/tasks/{taskId:guid}/attachments")]
public class TaskAttachmentsController : ApiControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public TaskAttachmentsController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AttachmentDto>>> GetAll(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var attachments = await _attachmentService.GetByTaskAsync(projectId, taskId, cancellationToken);
        return Ok(attachments);
    }

    [HttpPost]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid projectId, Guid taskId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var attachment = await _attachmentService.UploadAsync(projectId, taskId, content, file.FileName, file.ContentType, cancellationToken);
        return Ok(attachment);
    }

    [HttpGet("{attachmentId:guid}/content")]
    public async Task<IActionResult> Download(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var content = await _attachmentService.DownloadAsync(projectId, taskId, attachmentId, cancellationToken);
        return File(content.Content, content.ContentType, content.FileName);
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken cancellationToken)
    {
        await _attachmentService.DeleteAsync(projectId, taskId, attachmentId, cancellationToken);
        return NoContent();
    }
}
