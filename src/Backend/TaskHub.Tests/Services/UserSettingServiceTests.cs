using TaskHub.Application.Common;
using TaskHub.Application.Dtos.UserSettings;
using TaskHub.Application.Interfaces;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class UserSettingServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IUserSettingService _settings;

    public UserSettingServiceTests()
    {
        _settings = _host.Get<IUserSettingService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Set_CreatesThenOverwritesTheValue()
    {
        _host.SignIn(await _host.CreateUserAsync("anna"));

        await _settings.SetAsync("theme", new SetUserSettingRequest { Value = "light" });
        await _settings.SetAsync("theme", new SetUserSettingRequest { Value = "dark" });
        var all = await _settings.GetAllAsync();

        Assert.Equal("dark", Assert.Single(all).Value);
    }

    [Fact]
    public async Task Settings_AreVisibleOnlyToTheirOwner()
    {
        var anna = await _host.CreateUserAsync("anna");
        var jan = await _host.CreateUserAsync("jan");
        _host.SignIn(anna);
        await _settings.SetAsync("theme", new SetUserSettingRequest { Value = "dark" });

        _host.SignIn(jan);

        Assert.Empty(await _settings.GetAllAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => _settings.GetAsync("theme"));
        await Assert.ThrowsAsync<NotFoundException>(() => _settings.DeleteAsync("theme"));
    }

    [Fact]
    public async Task Set_RejectsTooLongKeysAndValues()
    {
        _host.SignIn(await _host.CreateUserAsync("anna"));

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _settings.SetAsync(new string('k', 101), new SetUserSettingRequest { Value = "value" }));
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _settings.SetAsync("key", new SetUserSettingRequest { Value = new string('v', 4001) }));
    }

    [Fact]
    public async Task Delete_RemovesTheSetting()
    {
        _host.SignIn(await _host.CreateUserAsync("anna"));
        await _settings.SetAsync("theme", new SetUserSettingRequest { Value = "dark" });

        await _settings.DeleteAsync("theme");

        Assert.Empty(await _settings.GetAllAsync());
    }
}
