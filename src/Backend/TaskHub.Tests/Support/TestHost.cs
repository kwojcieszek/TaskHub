using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Application;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Application.Services;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Infrastructure.Services;

namespace TaskHub.Tests.Support;

/// <summary>
/// Wires the real application services to a private in-memory SQLite database.
/// Only what lives outside the process is replaced: the HTTP user, file storage and Google.
/// </summary>
public sealed class TestHost : IDisposable
{
    public const string DefaultPassword = "Password1";
    public const long MaxFileSizeBytes = 1024;

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public TestHost(Action<SecurityOptions>? configureSecurity = null, Action<AttachmentStorageOptions>? configureAttachments = null)
    {
        // The database lives as long as this connection stays open.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDataProtection();
        services.AddApplication();

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireLowercase = false;
                options.User.RequireUniqueEmail = true;
                options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddPasswordValidator<ConfigurablePasswordValidator>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(options =>
        {
            options.Issuer = "TaskHub.Tests";
            options.Audience = "TaskHub.Tests.Client";
            options.SigningKey = "unit-test-signing-key-that-is-long-enough-0123456789";
        });
        services.Configure<SecurityOptions>(options => configureSecurity?.Invoke(options));
        services.Configure<AttachmentStorageOptions>(options =>
        {
            options.MaxFileSizeBytes = MaxFileSizeBytes;
            configureAttachments?.Invoke(options);
        });

        services.AddSingleton<FakeCurrentUserService>();
        services.AddSingleton<ICurrentUserService>(provider => provider.GetRequiredService<FakeCurrentUserService>());
        services.AddSingleton<InMemoryAttachmentStorage>();
        services.AddSingleton<IAttachmentStorage>(provider => provider.GetRequiredService<InMemoryAttachmentStorage>());
        services.AddSingleton<FakeGoogleTokenValidator>();
        services.AddSingleton<IGoogleTokenValidator>(provider => provider.GetRequiredService<FakeGoogleTokenValidator>());
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        Db.Database.EnsureCreated();

        var roleManager = Get<RoleManager<Role>>();
        roleManager.CreateAsync(new Role(Roles.Admin)).GetAwaiter().GetResult();
        roleManager.CreateAsync(new Role(Roles.User)).GetAwaiter().GetResult();
    }

    public ApplicationDbContext Db => Get<ApplicationDbContext>();

    public FakeCurrentUserService CurrentUser => Get<FakeCurrentUserService>();

    public InMemoryAttachmentStorage Storage => Get<InMemoryAttachmentStorage>();

    public UserManager<User> Users => Get<UserManager<User>>();

    public T Get<T>() where T : notnull
    {
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

    public async Task<User> CreateUserAsync(string userName, params string[] roles)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = $"{userName}@example.test",
            DisplayName = userName
        };

        var result = await Users.CreateAsync(user, DefaultPassword);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));

        foreach (var role in roles)
        {
            await Users.AddToRoleAsync(user, role);
        }

        return user;
    }

    /// <summary>Makes the given user the caller of every following service call.</summary>
    public void SignIn(User user, params string[] roles)
    {
        CurrentUser.SignIn(user.Id, roles);
    }

    /// <summary>Creates a project owned by the given user and leaves that user signed in.</summary>
    public async Task<ProjectDto> CreateProjectAsync(User owner, string name = "Project")
    {
        SignIn(owner);

        return await Get<IProjectService>().CreateAsync(new CreateProjectRequest { Name = name });
    }

    /// <summary>Adds a member to a project on behalf of its owner, then signs the owner back in.</summary>
    public async Task AddMemberAsync(ProjectDto project, User owner, User member, ProjectMemberRole role)
    {
        SignIn(owner);

        await Get<IProjectService>().AddMemberAsync(project.Id, new AddProjectMemberRequest { UserId = member.Id, Role = role });
    }

    /// <summary>Creates a team (a user group) with the given members, the way an administrator does.</summary>
    public async Task<UserGroupDto> CreateTeamAsync(string name, params User[] members)
    {
        var groups = Get<IUserGroupService>();
        var team = await groups.CreateAsync(new CreateUserGroupRequest { Name = name });

        foreach (var member in members)
        {
            await groups.AddMemberAsync(team.Id, new AddUserGroupMemberRequest { UserId = member.Id });
        }

        return team;
    }

    /// <summary>Gives a team access to a project on behalf of its owner, who stays signed in.</summary>
    public async Task AddTeamAsync(ProjectDto project, User owner, UserGroupDto team, ProjectMemberRole role)
    {
        SignIn(owner);

        await Get<IProjectTeamService>().AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = team.Id, Role = role });
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }
}
