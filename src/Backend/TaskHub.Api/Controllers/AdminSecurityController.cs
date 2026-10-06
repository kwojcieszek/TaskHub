using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Security;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize(Roles = Roles.Admin)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/security")]
public class AdminSecurityController : ApiControllerBase
{
    private readonly ISecuritySettingsService _securitySettingsService;

    public AdminSecurityController(ISecuritySettingsService securitySettingsService)
    {
        _securitySettingsService = securitySettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<SecuritySettingsDto>> Get(CancellationToken cancellationToken)
    {
        var settings = await _securitySettingsService.GetAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<SecuritySettingsDto>> Update(SecuritySettingsDto request, CancellationToken cancellationToken)
    {
        var settings = await _securitySettingsService.UpdateAsync(request, cancellationToken);
        return Ok(settings);
    }
}
