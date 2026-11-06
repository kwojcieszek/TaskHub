using System.Text;
using Microsoft.AspNetCore.Identity;
using TaskHub.Application.Common;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;

namespace TaskHub.Api;

public static class StartupSecurity
{
    private const string DefaultAdminPassword = "admin";
    private const string SigningKeyPlaceholder = "CHANGE_ME";
    private const int MinSigningKeyBytes = 32;
    private const int MinAdminPasswordLength = 12;

    public static async Task ValidateAccountsAsync(UserManager<User> users)
    {
        foreach (var admin in await users.GetUsersInRoleAsync(Roles.Admin))
        {
            if (await users.CheckPasswordAsync(admin, DefaultAdminPassword))
            {
                throw new InvalidOperationException("Production cannot start while an administrator account uses the published default password. Replace it before deployment.");
            }
        }
    }

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
        var admin = configuration.GetSection(DefaultAdminOptions.SectionName).Get<DefaultAdminOptions>() ?? new();

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < MinSigningKeyBytes ||
            jwt.SigningKey.Contains(SigningKeyPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Production requires a private JWT signing key of at least 32 bytes (Jwt__SigningKey).");
        }

        if (string.IsNullOrWhiteSpace(admin.Password) || admin.Password.Equals(DefaultAdminPassword, StringComparison.OrdinalIgnoreCase) ||
            admin.Password.Length < MinAdminPasswordLength)
        {
            throw new InvalidOperationException("Production requires an individual bootstrap administrator password of at least 12 characters (DefaultAdmin__Password).");
        }
    }
}
