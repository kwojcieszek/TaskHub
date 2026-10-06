using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Controllers;
using TaskHub.Application.Interfaces;

namespace TaskHub.Api.Middleware;

public class UserActivityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserActivityMiddleware> _logger;

    public UserActivityMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<UserActivityMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        await RecordAsync(context);
    }

    private async Task RecordAsync(HttpContext context)
    {
        if (!IsTrackedMethod(context.Request.Method) || context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var action = descriptor is null ? path : $"{descriptor.ControllerName}.{descriptor.ActionName}";

        try
        {
            // A separate scope keeps the audit entry independent of whatever state the request left in its DbContext.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

            await auditLogService.RecordActivityAsync(
                userId,
                context.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                action,
                context.Request.Method,
                path,
                context.Response.StatusCode);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not record user activity for {Path}", path);
        }
    }

    private static bool IsTrackedMethod(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }
}
