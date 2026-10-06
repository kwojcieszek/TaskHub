using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Security;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class SecuritySettingsServiceTests : IDisposable
{
    private readonly TestHost _host = new(ConfigureDefaults);
    private readonly ISecuritySettingsService _settings;

    public SecuritySettingsServiceTests()
    {
        _settings = _host.Get<ISecuritySettingsService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Get_FallsBackToConfiguredDefaultsWhenNothingWasSaved()
    {
        var settings = await _settings.GetAsync();

        Assert.True(settings.LockoutEnabled);
        Assert.Equal(7, settings.MaxFailedAccessAttempts);
        Assert.Equal(20, settings.LockoutMinutes);
        Assert.False(settings.RegistrationEnabled);
    }

    [Fact]
    public async Task Update_SavedValuesWinOverTheDefaults()
    {
        await _settings.UpdateAsync(new SecuritySettingsDto
        {
            LockoutEnabled = false,
            MaxFailedAccessAttempts = 2,
            LockoutMinutes = 5,
            RegistrationEnabled = true
        });

        var settings = await _settings.GetAsync();

        Assert.False(settings.LockoutEnabled);
        Assert.Equal(2, settings.MaxFailedAccessAttempts);
        Assert.Equal(5, settings.LockoutMinutes);
        Assert.True(settings.RegistrationEnabled);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(101, 15)]
    [InlineData(5, 0)]
    [InlineData(5, 525_601)]
    public async Task Update_RejectsValuesOutsideTheAllowedRange(int attempts, int minutes)
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => _settings.UpdateAsync(new SecuritySettingsDto
        {
            LockoutEnabled = true,
            MaxFailedAccessAttempts = attempts,
            LockoutMinutes = minutes
        }));
    }

    private static void ConfigureDefaults(SecurityOptions options)
    {
        options.LockoutEnabled = true;
        options.MaxFailedAccessAttempts = 7;
        options.LockoutMinutes = 20;
        options.RegistrationEnabled = false;
    }
}
