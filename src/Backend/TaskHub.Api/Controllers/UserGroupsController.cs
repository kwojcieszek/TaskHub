using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/user-groups")]
public class UserGroupsController : ApiControllerBase
{
    private readonly IUserGroupService _userGroupService;

    public UserGroupsController(IUserGroupService userGroupService)
    {
        _userGroupService = userGroupService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserGroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await _userGroupService.GetAllAsync(cancellationToken);
        return Ok(groups);
    }

    [HttpPut("{groupId:guid}/owner")]
    public async Task<ActionResult<UserGroupDto>> TransferOwner(Guid groupId, TransferTeamOwnershipRequest request, CancellationToken ct)
        => Ok(await _userGroupService.TransferOwnershipAsync(groupId, request.UserId, ct));

    [HttpPost("{groupId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid groupId, CancellationToken ct)
    {
        await _userGroupService.LeaveAsync(groupId, ct);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<UserGroupDto>> Create(CreateUserGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await _userGroupService.CreateAsync(request, cancellationToken);
        return Ok(group);
    }

    [HttpPut("{groupId:guid}")]
    public async Task<ActionResult<UserGroupDto>> Update(Guid groupId, UpdateUserGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await _userGroupService.UpdateAsync(groupId, request, cancellationToken);
        return Ok(group);
    }

    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> Delete(Guid groupId, CancellationToken cancellationToken)
    {
        await _userGroupService.DeleteAsync(groupId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{groupId:guid}/members")]
    public async Task<ActionResult<IReadOnlyCollection<UserGroupMemberDto>>> GetMembers(Guid groupId, CancellationToken cancellationToken)
    {
        var members = await _userGroupService.GetMembersAsync(groupId, cancellationToken);
        return Ok(members);
    }

    [HttpPost("{groupId:guid}/members")]
    public async Task<ActionResult<UserGroupMemberDto>> AddMember(Guid groupId, AddUserGroupMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await _userGroupService.AddMemberAsync(groupId, request, cancellationToken);
        return Ok(member);
    }

    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid userId, CancellationToken cancellationToken)
    {
        await _userGroupService.RemoveMemberAsync(groupId, userId, cancellationToken);
        return NoContent();
    }
}
