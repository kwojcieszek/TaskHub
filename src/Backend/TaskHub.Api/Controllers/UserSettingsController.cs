using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.UserSettings;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/settings")]
public class UserSettingsController : ApiControllerBase
{
    private readonly IUserSettingService _userSettingService;

    public UserSettingsController(IUserSettingService userSettingService)
    {
        _userSettingService = userSettingService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserSettingDto>>> GetAll(CancellationToken cancellationToken)
    {
        var settings = await _userSettingService.GetAllAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<UserSettingDto>> Get(string key, CancellationToken cancellationToken)
    {
        var setting = await _userSettingService.GetAsync(key, cancellationToken);
        return Ok(setting);
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<UserSettingDto>> Set(string key, SetUserSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = await _userSettingService.SetAsync(key, request, cancellationToken);
        return Ok(setting);
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        await _userSettingService.DeleteAsync(key, cancellationToken);
        return NoContent();
    }
}
