using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Application.Services;
using TaskHub.Domain.Entities;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Infrastructure.Seed;
using TaskHub.Infrastructure.Services;
using TaskHub.Infrastructure.Storage;

namespace TaskHub.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<DefaultAdminOptions>(configuration.GetSection(DefaultAdminOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        services.AddDbContext<ApplicationDbContext>(options => ConfigureProvider(options, databaseOptions));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddIdentityCore<User>(options =>
            {
                // The rules live in the security settings and are applied by ConfigurablePasswordValidator;
                // the built-in checks are switched off so the two cannot disagree.
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

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<DatabaseSchemaUpdater>();
        services.AddScoped<DbSeeder>();

        services.Configure<AttachmentStorageOptions>(configuration.GetSection(AttachmentStorageOptions.SectionName));

        var attachmentStorageOptions = configuration.GetSection(AttachmentStorageOptions.SectionName).Get<AttachmentStorageOptions>()
            ?? new AttachmentStorageOptions();

        switch (attachmentStorageOptions.Provider)
        {
            case AttachmentStorageProvider.Ftp:
                services.AddSingleton<IAttachmentStorage, FtpAttachmentStorage>();
                break;
            case AttachmentStorageProvider.FileSystem:
            default:
                services.AddSingleton<IAttachmentStorage, FileSystemAttachmentStorage>();
                break;
        }

        return services;
    }

    private static void ConfigureProvider(DbContextOptionsBuilder options, DatabaseOptions databaseOptions)
    {
        switch (databaseOptions.Provider)
        {
            case DatabaseProvider.Postgresql:
                options.UseNpgsql(databaseOptions.ConnectionString);
                break;
            case DatabaseProvider.MySql:
                options.UseMySQL(databaseOptions.ConnectionString);
                break;
            case DatabaseProvider.MsSql:
                options.UseSqlServer(databaseOptions.ConnectionString);
                break;
            case DatabaseProvider.Sqlite:
            default:
                options.UseSqlite(databaseOptions.ConnectionString);
                break;
        }
    }
}
