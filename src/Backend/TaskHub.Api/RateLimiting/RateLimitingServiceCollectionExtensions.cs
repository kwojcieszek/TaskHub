using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using TaskHub.Application.Options;

namespace TaskHub.Api.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    private const string UnknownClient = "unknown";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitingOptions = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Every request counts against its caller: the signed-in user, or the IP address when anonymous.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                CreatePartition(GetCallerKey(context), rateLimitingOptions, rateLimitingOptions.PermitLimit));

            // Login, registration and 2FA get a much tighter per-address limit on top of the global one.
            options.AddPolicy(RateLimitPolicies.Auth, context =>
                CreatePartition(GetIpAddress(context), rateLimitingOptions, rateLimitingOptions.AuthPermitLimit));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                response.ContentType = "application/json";

                var payload = JsonSerializer.Serialize(new { errors = new[] { "Too many requests. Please try again later." } });
                await response.WriteAsync(payload, cancellationToken);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(string key, RateLimitingOptions rateLimitingOptions, int permitLimit)
    {
        if (!rateLimitingOptions.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(key);
        }

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(rateLimitingOptions.WindowSeconds),
            QueueLimit = 0
        });
    }

    private static string GetCallerKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? $"ip:{GetIpAddress(context)}" : $"user:{userId}";
    }

    private static string GetIpAddress(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;
    }
}
