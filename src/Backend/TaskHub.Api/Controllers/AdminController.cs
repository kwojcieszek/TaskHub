using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize(Roles = Roles.Admin)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
public class AdminController : ApiControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AdminUserDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _adminUserService.GetAllUsersAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPut("{userId:guid}/roles")]
    public async Task<ActionResult<AdminUserDto>> UpdateRoles(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await _adminUserService.UpdateRolesAsync(userId, request, cancellationToken);
        return Ok(user);
    }

    [HttpPut("{userId:guid}/lockout")]
    public async Task<ActionResult<AdminUserDto>> SetLockout(Guid userId, SetUserLockoutRequest request, CancellationToken cancellationToken)
    {
        var user = await _adminUserService.SetLockoutAsync(userId, request, cancellationToken);
        return Ok(user);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        await _adminUserService.DeleteUserAsync(userId, cancellationToken);
        return NoContent();
    }
}
