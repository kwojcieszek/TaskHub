using Microsoft.Extensions.DependencyInjection;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Services;

namespace TaskHub.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(_ => { }, typeof(ApplicationServiceCollectionExtensions).Assembly);

        services.AddScoped<IProjectAccessGuard, ProjectAccessGuard>();
        services.AddScoped<IProjectGroupService, ProjectGroupService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskItemService, TaskItemService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IUserGroupService, UserGroupService>();
        services.AddScoped<IUserSettingService, UserSettingService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ISecuritySettingsService, SecuritySettingsService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserDirectoryService, UserDirectoryService>();

        return services;
    }
}
