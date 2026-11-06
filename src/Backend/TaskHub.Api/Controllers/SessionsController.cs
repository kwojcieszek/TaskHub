using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Api.Authentication;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.InteractiveSession)]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/sessions")]
public class SessionsController : ApiControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SessionDto>>> Get(CancellationToken cancellationToken)
    {
        var sessions = await _sessionService.GetMineAsync(cancellationToken);

        return Ok(sessions);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await _sessionService.RevokeAsync(id, cancellationToken);

        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] bool all, CancellationToken cancellationToken)
    {
        await _sessionService.LogoutAsync(all, cancellationToken);

        return NoContent();
    }
}
