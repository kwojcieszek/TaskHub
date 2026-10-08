using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Dtos.Common;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize(Roles = Roles.Admin)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/logs")]
public class AdminLogsController : ApiControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AdminLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet("logins")]
    public async Task<ActionResult<PagedResultDto<UserActivityLogDto>>> GetLogins([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = await _auditLogService.GetLoginLogsAsync(query, cancellationToken);
        return Ok(logs);
    }

    [HttpGet("activity")]
    public async Task<ActionResult<PagedResultDto<UserActivityLogDto>>> GetActivity([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = await _auditLogService.GetActivityLogsAsync(query, cancellationToken);
        return Ok(logs);
    }
}
