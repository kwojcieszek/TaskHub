using System.Net;
using TaskHub.Application.Dtos.Auth;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Integration;

/// <summary>
/// Guards the test setup itself: the API must run against its own private database and settings.
/// </summary>
public class ApiHostTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public ApiHostTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Api_UsesThePrivateDatabaseOfTheTestRun()
    {
        var response = await _factory.Anonymous().GetAsync(ApiRoutes.Providers);

        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.True(File.Exists(_factory.DatabasePath));
    }

    [Fact]
    public void Api_UsesTheTestSettings()
    {
        Assert.Equal("TaskHub.IntegrationTests", _factory.Jwt.Issuer);
    }

    [Fact]
    public async Task Providers_ArePublicAndReportThePasswordPolicy()
    {
        var providers = await _factory.Anonymous().GetAsync<AuthProvidersDto>(ApiRoutes.Providers);

        Assert.False(providers.RegistrationEnabled);
        Assert.Equal(8, providers.PasswordPolicy.MinLength);
    }

    [Fact]
    public async Task SeededAdministrator_CanSignInAndReachTheAdminPanel()
    {
        var admin = await _factory.AdminAsync();

        var response = await admin.GetAsync(ApiRoutes.AdminUsers);

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatedUser_IsSignedInAsAnOrdinaryUser()
    {
        var user = await _factory.CreateUserAsync();

        var me = await user.GetAsync<UserDto>(ApiRoutes.Me);

        Assert.Equal(user.UserId, me.Id);
        Assert.False(me.MustChangePassword);
        Assert.Equal(new[] { "User" }, me.Roles);
    }
}
