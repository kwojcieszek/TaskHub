using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskHub.Api.Authentication;
using TaskHub.Application.Dtos.ApiTokens;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

// The tokens of the signed-in user. An API token cannot be used here, so a leaked one cannot mint others.
[Authorize(Policy = AuthorizationPolicies.InteractiveSession)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tokens")]
public class ApiTokensController : ApiControllerBase
{
    private readonly IApiTokenService _apiTokenService;

    public ApiTokensController(IApiTokenService apiTokenService)
    {
        _apiTokenService = apiTokenService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ApiTokenDto>>> GetAll(CancellationToken cancellationToken)
    {
        var tokens = await _apiTokenService.GetMineAsync(cancellationToken);
        return Ok(tokens);
    }

    [HttpPost]
    public async Task<ActionResult<CreatedApiTokenDto>> Create(CreateApiTokenRequest request, CancellationToken cancellationToken)
    {
        var token = await _apiTokenService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, token);
    }

    [HttpDelete("{tokenId:guid}")]
    public async Task<IActionResult> Revoke(Guid tokenId, CancellationToken cancellationToken)
    {
        await _apiTokenService.RevokeAsync(tokenId, cancellationToken);
        return NoContent();
    }
}
