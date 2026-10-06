using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Security;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class SecuritySettingsService : ISecuritySettingsService
{
    private const string KeyPrefix = "Security.";
    private const string LockoutEnabledKey = KeyPrefix + "LockoutEnabled";
    private const string MaxFailedAccessAttemptsKey = KeyPrefix + "MaxFailedAccessAttempts";
    private const string LockoutMinutesKey = KeyPrefix + "LockoutMinutes";

    private const int MaxAllowedAttempts = 100;
    private const int MaxAllowedLockoutMinutes = 525_600;

    private readonly IApplicationDbContext _dbContext;
    private readonly SecurityOptions _defaults;

    public SecuritySettingsService(IApplicationDbContext dbContext, IOptions<SecurityOptions> defaults)
    {
        _dbContext = dbContext;
        _defaults = defaults.Value;
    }

    public async Task<SecuritySettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _dbContext.SystemSettings
            .AsNoTracking()
            .Where(s => s.Key.StartsWith(KeyPrefix))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        // Values saved by an administrator win; anything not saved yet falls back to appsettings.
        return new SecuritySettingsDto
        {
            LockoutEnabled = stored.TryGetValue(LockoutEnabledKey, out var enabled) && bool.TryParse(enabled, out var parsedEnabled)
                ? parsedEnabled
                : _defaults.LockoutEnabled,
            MaxFailedAccessAttempts = ReadInt(stored, MaxFailedAccessAttemptsKey, _defaults.MaxFailedAccessAttempts),
            LockoutMinutes = ReadInt(stored, LockoutMinutesKey, _defaults.LockoutMinutes)
        };
    }

    public async Task<SecuritySettingsDto> UpdateAsync(SecuritySettingsDto request, CancellationToken cancellationToken = default)
    {
        if (request.MaxFailedAccessAttempts < 1 || request.MaxFailedAccessAttempts > MaxAllowedAttempts)
        {
            throw new ValidationFailedException(new[] { $"The number of failed attempts must be between 1 and {MaxAllowedAttempts}." });
        }

        if (request.LockoutMinutes < 1 || request.LockoutMinutes > MaxAllowedLockoutMinutes)
        {
            throw new ValidationFailedException(new[] { $"The lockout time must be between 1 and {MaxAllowedLockoutMinutes} minutes." });
        }

        var values = new Dictionary<string, string>
        {
            [LockoutEnabledKey] = request.LockoutEnabled.ToString(),
            [MaxFailedAccessAttemptsKey] = request.MaxFailedAccessAttempts.ToString(CultureInfo.InvariantCulture),
            [LockoutMinutesKey] = request.LockoutMinutes.ToString(CultureInfo.InvariantCulture)
        };

        var stored = await _dbContext.SystemSettings
            .Where(s => s.Key.StartsWith(KeyPrefix))
            .ToListAsync(cancellationToken);

        foreach (var (key, value) in values)
        {
            var setting = stored.FirstOrDefault(s => s.Key == key);

            if (setting is null)
            {
                setting = new SystemSetting { Key = key };
                _dbContext.SystemSettings.Add(setting);
            }

            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> stored, string key, int fallback)
    {
        return stored.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}
