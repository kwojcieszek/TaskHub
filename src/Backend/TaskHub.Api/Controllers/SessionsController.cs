using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Api.Authentication;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.InteractiveSession)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth/sessions")]
public class SessionsController(ISessionService sessions) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SessionDto>>> Get(CancellationToken ct) => Ok(await sessions.GetMineAsync(ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await sessions.RevokeAsync(id, ct);
        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] bool all, CancellationToken ct)
    {
        await sessions.LogoutAsync(all, ct);
        return NoContent();
    }
}
