using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskHub.Api.RateLimiting;
using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Common;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuditLogService _auditLogService;

    public AuthController(IAuthService authService, IAuditLogService auditLogService)
    {
        _authService = authService;
        _auditLogService = auditLogService;
    }

    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }

    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("2fa/verify")]
    public async Task<ActionResult<LoginResponse>> VerifyTwoFactor(TwoFactorVerifyRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyTwoFactorAsync(request, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupResponse>> BeginTwoFactorSetup(CancellationToken cancellationToken)
    {
        var response = await _authService.BeginTwoFactorSetupAsync(CurrentUserId, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorEnableRequest request, CancellationToken cancellationToken)
    {
        await _authService.EnableTwoFactorAsync(CurrentUserId, request.Code, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(CancellationToken cancellationToken)
    {
        await _authService.DisableTwoFactorAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(CurrentUserId, cancellationToken);
        return Ok(user);
    }

    [HttpGet("providers")]
    public ActionResult<AuthProvidersDto> Providers()
    {
        return Ok(_authService.GetProviders());
    }

    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("google/login")]
    public async Task<ActionResult<LoginResponse>> LoginWithGoogle(GoogleTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginWithGoogleAsync(request, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("google/link")]
    public async Task<ActionResult<UserDto>> LinkGoogle(GoogleTokenRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.LinkGoogleAsync(CurrentUserId, request, cancellationToken);
        return Ok(user);
    }

    [Authorize]
    [HttpDelete("google/link")]
    public async Task<ActionResult<UserDto>> UnlinkGoogle(CancellationToken cancellationToken)
    {
        var user = await _authService.UnlinkGoogleAsync(CurrentUserId, cancellationToken);
        return Ok(user);
    }

    [Authorize]
    [HttpGet("logins")]
    public async Task<ActionResult<PagedResultDto<LoginLogDto>>> MyLogins([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = await _auditLogService.GetLoginLogsForUserAsync(CurrentUserId, query, cancellationToken);
        return Ok(logs);
    }
}
