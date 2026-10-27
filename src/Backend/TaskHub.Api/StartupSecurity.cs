using Microsoft.AspNetCore.Identity;
using TaskHub.Domain.Entities;
using TaskHub.Application.Common;
using TaskHub.Application.Options;

namespace TaskHub.Api;

public static class StartupSecurity
{
    public static async Task ValidateAccountsAsync(UserManager<User> users)
    {
        foreach (var admin in await users.GetUsersInRoleAsync(Roles.Admin))
            if (await users.CheckPasswordAsync(admin, "admin"))
                throw new InvalidOperationException("Production cannot start while an administrator account uses the published default password. Replace it before deployment.");
    }

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction()) return;
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
        var admin = configuration.GetSection(DefaultAdminOptions.SectionName).Get<DefaultAdminOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || System.Text.Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32 ||
            jwt.SigningKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production requires a private JWT signing key of at least 32 bytes (Jwt__SigningKey).");
        if (string.IsNullOrWhiteSpace(admin.Password) || admin.Password.Equals("admin", StringComparison.OrdinalIgnoreCase) || admin.Password.Length < 12)
            throw new InvalidOperationException("Production requires an individual bootstrap administrator password of at least 12 characters (DefaultAdmin__Password).");
        var storage = configuration.GetSection(AttachmentStorageOptions.SectionName).Get<AttachmentStorageOptions>() ?? new();
    }
}
