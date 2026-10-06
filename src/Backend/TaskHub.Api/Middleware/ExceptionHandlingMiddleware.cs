using System.Net;
using System.Text.Json;
using TaskHub.Application.Common;

namespace TaskHub.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errors) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, new[] { exception.Message }),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, new[] { exception.Message }),
            ConflictException => (HttpStatusCode.Conflict, new[] { exception.Message }),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, new[] { exception.Message }),
            ValidationFailedException validationFailedException => (HttpStatusCode.BadRequest, validationFailedException.Errors.ToArray()),
            _ => (HttpStatusCode.InternalServerError, new[] { "An unexpected error occurred." })
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Path}", context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = JsonSerializer.Serialize(new { errors });
        await context.Response.WriteAsync(payload);
    }
}
