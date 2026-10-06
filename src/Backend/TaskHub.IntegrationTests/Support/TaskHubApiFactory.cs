using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskHub.Api.Controllers;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Options;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Runs the real API in memory: the whole pipeline, the real services and a private SQLite database
/// with its own attachment folder, both removed when the instance is disposed.
/// </summary>
public class TaskHubApiFactory : WebApplicationFactory<ApiControllerBase>
{
    public const string AdminUserName = "admin";
    public const string InitialAdminPassword = "admin";
    public const string AdminPassword = "Admin-Passw0rd-1";

    // The password an administrator assigns, and the one the user replaces it with.
    public const string AssignedPassword = "Assigned-Passw0rd-1";
    public const string UserPassword = "User-Passw0rd-1";

    public const long MaxFileSizeBytes = 4096;
    public const int MaxFailedAccessAttempts = 3;

    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), "taskhub-integration-tests", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ApiSession? _admin;
    private TestCast? _cast;
    private int _userCounter;

    public TaskHubApiFactory()
    {
        _settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={DatabasePath};Pooling=False",
            ["AttachmentStorage:Provider"] = "FileSystem",
            ["AttachmentStorage:FileSystem:RootPath"] = AttachmentsPath,
            ["AttachmentStorage:MaxFileSizeBytes"] = MaxFileSizeBytes.ToString(),
            ["AttachmentStorage:MaxTotalBytesPerUser"] = "0",
            ["Jwt:Issuer"] = "TaskHub.IntegrationTests",
            ["Jwt:Audience"] = "TaskHub.IntegrationTests.Client",
            ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789-abcdefghijklmnop",
            ["Jwt:AccessTokenMinutes"] = "60",
            ["Security:LockoutEnabled"] = "true",
            ["Security:MaxFailedAccessAttempts"] = MaxFailedAccessAttempts.ToString(),
            ["Security:LockoutMinutes"] = "15",
            ["Security:RegistrationEnabled"] = "false",
            ["Security:PasswordPolicy:MinLength"] = "8",
            ["Security:PasswordPolicy:RequireUppercase"] = "true",
            ["Security:PasswordPolicy:RequireLowercase"] = "true",
            ["Security:PasswordPolicy:RequireDigit"] = "true",
            ["Security:PasswordPolicy:RequireNonAlphanumeric"] = "false",
            ["GoogleAuth:ClientId"] = "",
            // Off by default, so signing many users in does not trip the limiter; the rate limiting tests switch it on.
            ["RateLimiting:Enabled"] = "false",
            ["ReverseProxy:Enabled"] = "false",
            ["DefaultAdmin:UserName"] = AdminUserName,
            ["DefaultAdmin:Password"] = InitialAdminPassword,
            ["DefaultAdmin:Email"] = "admin@taskhub.test"
        };
    }

    public string DatabasePath => Path.Combine(_workDirectory, "taskhub.db");

    public string AttachmentsPath => Path.Combine(_workDirectory, "attachments");

    public JwtOptions Jwt => Services.GetRequiredService<IOptions<JwtOptions>>().Value;

    /// <summary>Overrides one configuration value. Only has an effect before the first request starts the API.</summary>
    public TaskHubApiFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;

        return this;
    }

    public ApiSession Anonymous()
    {
        return new ApiSession(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
    }

    public ApiSession WithToken(string token)
    {
        var session = Anonymous();
        session.Authenticate(token);

        return session;
    }

    /// <summary>The seeded administrator, past the forced change of the default password.</summary>
    public async Task<ApiSession> AdminAsync()
    {
        await _gate.WaitAsync();

        try
        {
            return await EnsureAdminAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TestCast> GetCastAsync()
    {
        await _gate.WaitAsync();

        try
        {
            if (_cast is null)
            {
                var admin = await EnsureAdminAsync();
                var owner = await CreateActiveUserAsync(admin, "owner");
                var editor = await CreateActiveUserAsync(admin, "editor");
                var viewer = await CreateActiveUserAsync(admin, "viewer");
                var stranger = await CreateActiveUserAsync(admin, "stranger");

                _cast = new TestCast(Anonymous(), admin, owner, editor, viewer, stranger);
            }

            return _cast;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Creates a user the way an administrator does: the account exists, but still holds the assigned password.</summary>
    public async Task<ApiSession> CreatePendingUserAsync(string prefix = "user")
    {
        var admin = await AdminAsync();

        return await CreatePendingUserAsync(admin, prefix);
    }

    /// <summary>Creates a user who already replaced the assigned password and is signed in.</summary>
    public async Task<ApiSession> CreateUserAsync(string prefix = "user")
    {
        var admin = await AdminAsync();

        return await CreateActiveUserAsync(admin, prefix);
    }

    /// <summary>Creates a second administrator, signed in with a token that already carries the Admin role.</summary>
    public async Task<ApiSession> CreateAdminAsync(string prefix = "admin")
    {
        var admin = await AdminAsync();
        var session = await CreateActiveUserAsync(admin, prefix);

        var response = await admin.PutAsync(ApiRoutes.AdminUserRoles(session.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.Admin, Roles.User } });
        await response.ShouldBeAsync(HttpStatusCode.OK);

        // Changing roles ends the user's sessions, so the new role arrives with a fresh sign-in.
        await LoginAsync(session);

        return session;
    }

    /// <summary>Signs the session in with its own user name and password and keeps the access token, if one is issued.</summary>
    public async Task<LoginResponse> LoginAsync(ApiSession session)
    {
        session.SignOut();

        var login = await session.PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = session.UserName, Password = session.Password });

        if (login.AccessToken is not null)
        {
            session.Authenticate(login.AccessToken);
        }

        return login;
    }

    /// <summary>A separate client for an existing user, signed in on its own.</summary>
    public async Task<ApiSession> LoginAsync(string userName, string password)
    {
        var session = Anonymous();
        session.UserName = userName;
        session.Password = password;

        var login = await LoginAsync(session);
        session.UserId = login.User?.Id ?? Guid.Empty;

        return session;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Directory.CreateDirectory(_workDirectory);

        // Host configuration reaches the application as command line arguments, so it is already there
        // when Program.cs reads its settings while building the services.
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(_settings));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureTestServices(services =>
        {
            // Hashing with the production work factor would make up most of the run time of the suite.
            services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1000);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        _gate.Dispose();

        try
        {
            if (Directory.Exists(_workDirectory))
            {
                Directory.Delete(_workDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temporary folder must not fail a test run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task<ApiSession> EnsureAdminAsync()
    {
        if (_admin is not null)
        {
            return _admin;
        }

        var session = Anonymous();
        session.UserName = AdminUserName;
        session.Password = InitialAdminPassword;

        var login = await LoginAsync(session);
        session.UserId = login.User!.Id;
        session.Email = login.User.Email;

        await ChangePasswordAsync(session, AdminPassword);

        _admin = session;

        return session;
    }

    private async Task<ApiSession> CreatePendingUserAsync(ApiSession admin, string prefix)
    {
        var userName = $"{prefix}{Interlocked.Increment(ref _userCounter)}x{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 50)];

        var request = new CreateUserRequest
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            DisplayName = $"Display {userName}",
            Password = AssignedPassword
        };

        var created = await admin.PostAsync<AdminUserDto>(ApiRoutes.AdminUsers, request, HttpStatusCode.Created);

        var session = Anonymous();
        session.UserId = created.Id;
        session.UserName = created.UserName;
        session.Email = created.Email;
        session.Password = AssignedPassword;

        return session;
    }

    private async Task<ApiSession> CreateActiveUserAsync(ApiSession admin, string prefix)
    {
        var session = await CreatePendingUserAsync(admin, prefix);

        await LoginAsync(session);
        await ChangePasswordAsync(session, UserPassword);

        return session;
    }

    private static async Task ChangePasswordAsync(ApiSession session, string newPassword)
    {
        var changed = await session.PostAsync<LoginResponse>(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = session.Password, NewPassword = newPassword });

        session.Password = newPassword;
        session.Authenticate(changed.AccessToken!);
    }
}
