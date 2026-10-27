using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/users")]
public class UsersController : ApiControllerBase
{
    private readonly IUserDirectoryService _userDirectoryService;

    public UsersController(IUserDirectoryService userDirectoryService)
    {
        _userDirectoryService = userDirectoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserSummaryDto>>> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var users = await _userDirectoryService.SearchAsync(q, cancellationToken);
        return Ok(users);
    }
}
