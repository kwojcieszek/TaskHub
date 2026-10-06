using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Security;
using TaskHub.Application.Interfaces;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class PasswordPolicyTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IAdminUserService _admin;
    private readonly ISecuritySettingsService _settings;

    public PasswordPolicyTests()
    {
        _admin = _host.Get<IAdminUserService>();
        _settings = _host.Get<ISecuritySettingsService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Theory]
    [InlineData("Short1A")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoDigitsHere")]
    public async Task DefaultPolicy_RejectsPasswordsThatBreakARule(string password)
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => CreateUserAsync(password));
    }

    [Fact]
    public async Task DefaultPolicy_AcceptsAPasswordThatMeetsEveryRule()
    {
        var created = await CreateUserAsync("Assigned1");

        Assert.Equal("anna", created.UserName);
    }

    [Fact]
    public async Task SavedPolicy_AppliesToTheNextPasswordAtOnce()
    {
        await SavePolicyAsync(new PasswordPolicyDto
        {
            MinLength = 4,
            RequireUppercase = false,
            RequireLowercase = false,
            RequireDigit = false,
            RequireNonAlphanumeric = false
        });

        var created = await CreateUserAsync("abcd");

        Assert.Equal("anna", created.UserName);
    }

    [Fact]
    public async Task SavedPolicy_CanRequireASpecialCharacter()
    {
        await SavePolicyAsync(new PasswordPolicyDto { RequireNonAlphanumeric = true });

        await Assert.ThrowsAsync<ValidationFailedException>(() => CreateUserAsync("Assigned1"));

        var created = await CreateUserAsync("Assigned1!");

        Assert.Equal("anna", created.UserName);
    }

    [Fact]
    public async Task SavedPolicy_AppliesToAPasswordChangeButNotToAnExistingPassword()
    {
        var user = await _host.CreateUserAsync("jan");
        var auth = _host.Get<IAuthService>();

        await SavePolicyAsync(new PasswordPolicyDto { MinLength = 12 });

        // The password set under the old policy still signs the user in.
        var login = await auth.LoginAsync(new LoginRequest { UserName = "jan", Password = TestHost.DefaultPassword });

        Assert.NotNull(login.AccessToken);

        await Assert.ThrowsAsync<ValidationFailedException>(() => auth.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = TestHost.DefaultPassword,
            NewPassword = "TooShort1"
        }));
    }

    [Fact]
    public async Task Providers_ExposeThePolicyToThePasswordForms()
    {
        await SavePolicyAsync(new PasswordPolicyDto { MinLength = 10, RequireNonAlphanumeric = true });

        var providers = await _host.Get<IAuthService>().GetProvidersAsync();

        Assert.Equal(10, providers.PasswordPolicy.MinLength);
        Assert.True(providers.PasswordPolicy.RequireNonAlphanumeric);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(65)]
    public async Task Update_RejectsAMinimumLengthOutsideTheAllowedRange(int minLength)
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() => SavePolicyAsync(new PasswordPolicyDto { MinLength = minLength }));
    }

    private async Task SavePolicyAsync(PasswordPolicyDto policy)
    {
        var settings = await _settings.GetAsync();
        settings.PasswordPolicy = policy;

        await _settings.UpdateAsync(settings);
    }

    private Task<AdminUserDto> CreateUserAsync(string password)
    {
        return _admin.CreateUserAsync(new CreateUserRequest
        {
            UserName = "anna",
            Email = "anna@example.test",
            Password = password
        });
    }
}
