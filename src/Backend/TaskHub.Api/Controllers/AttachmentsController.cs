using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/attachments")]
public class AttachmentsController : ApiControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentsController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet("{sha256}")]
    public async Task<ActionResult<IReadOnlyCollection<AttachmentDto>>> FindBySha256([FromQuery, BindRequired] Guid taskId, string sha256, CancellationToken cancellationToken)
    {
        var attachments = await _attachmentService.FindBySha256Async(taskId, sha256, cancellationToken);
        return Ok(attachments);
    }

    [HttpGet("{sha256}/content")]
    public async Task<IActionResult> DownloadBySha256([FromQuery, BindRequired] Guid taskId, string sha256, CancellationToken cancellationToken)
    {
        var content = await _attachmentService.DownloadBySha256Async(taskId, sha256, cancellationToken);
        return File(content.Content, content.ContentType, content.FileName);
    }
}
