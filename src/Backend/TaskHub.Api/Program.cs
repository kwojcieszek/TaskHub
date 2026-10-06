using System.Net;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TaskHub.Api.Authentication;
using TaskHub.Api.Middleware;
using TaskHub.Api.RateLimiting;
using TaskHub.Application;
using TaskHub.Application.Options;
using TaskHub.Infrastructure;
using TaskHub.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Validation failures use the same { errors: [...] } shape as every other error the API returns.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "The request is invalid." : error.ErrorMessage)
                .ToArray();

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { errors });
        };
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The largest request the server reads is one attachment plus the multipart framing around it,
// so AttachmentStorage:MaxFileSizeBytes is the single place where the upload limit is set.
const long MultipartOverheadBytes = 1024 * 1024;

var attachmentStorageOptions = builder.Configuration.GetSection(AttachmentStorageOptions.SectionName).Get<AttachmentStorageOptions>()
    ?? new AttachmentStorageOptions();
var maxRequestBodySize = attachmentStorageOptions.MaxFileSizeBytes + MultipartOverheadBytes;

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBodySize);
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = maxRequestBodySize);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBodySize);

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.EventsType = typeof(SessionJwtBearerEvents);
    });

builder.Services.AddScoped<SessionJwtBearerEvents>();

builder.Services.AddAuthorization();

builder.Services.AddApiRateLimiting(builder.Configuration);

const string AngularClientCorsPolicy = "AngularClient";

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientCorsPolicy, policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:4600" })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Behind a reverse proxy the connection comes from the proxy, so the client address is taken from X-Forwarded-For.
// Only the proxies listed in ReverseProxy (and the local machine) are believed; anyone else could forge the header.
var reverseProxyOptions = builder.Configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>()
    ?? new ReverseProxyOptions();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var proxy in reverseProxyOptions.KnownProxies)
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }

    foreach (var network in reverseProxyOptions.KnownNetworks)
    {
        options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(network));
    }
});

var app = builder.Build();

// First in the pipeline, so the audit log and the rate limiter below already see the real client address.
if (reverseProxyOptions.Enabled)
{
    app.UseForwardedHeaders();
}

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

// Registered first so it sees the final status code, including the ones set by the exception handler.
app.UseMiddleware<UserActivityMiddleware>();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "TaskHub API";
    });
}

app.UseHttpsRedirection();

app.UseCors(AngularClientCorsPolicy);

app.UseAuthentication();

app.UseMiddleware<PasswordChangeRequiredMiddleware>();

// After authentication, so signed-in callers are limited per user rather than per address.
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
