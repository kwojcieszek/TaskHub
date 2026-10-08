using System.Net;
using TaskHub.Application.Dtos.Admin;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// The directory lets a user find a colleague to add to a project. It must not work as a list of accounts:
/// nothing comes back without a search, and logins are shown to administrators only.
/// </summary>
public class UserDirectoryTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public UserDirectoryTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("")]
    [InlineData("?q=")]
    [InlineData("?q=%20%20%20")]
    [InlineData("?q=a")]
    [InlineData("?q=%25")]
    [InlineData("?q=_")]
    public async Task Directory_ReturnsNothingWithoutARealSearch(string query)
    {
        var user = await _factory.CreateUserAsync("dirlister");
        var admin = await _factory.AdminAsync();

        var asUser = await user.GetAsync<List<UserSummaryDto>>(ApiRoutes.Users + query);
        var asAdmin = await admin.GetAsync<List<UserSummaryDto>>(ApiRoutes.Users + query);

        Assert.Empty(asUser);
        Assert.Empty(asAdmin);
    }

    [Theory]
    [InlineData("%25%25")]
    [InlineData("__")]
    [InlineData("%25a%25")]
    public async Task Directory_TreatsWildcardsAsText(string query)
    {
        // Otherwise "%%" would match every account and read the whole directory out.
        var user = await _factory.CreateUserAsync("dirwildcard");

        var found = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={query}");

        Assert.Empty(found);
    }

    [Fact]
    public async Task Directory_ShowsLoginsToAdministratorsOnly()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("dirviewer");
        var target = await CreateNamedUserAsync("Zofia Kowalska");

        var response = await user.GetAsync($"{ApiRoutes.Users}?q=Kowalska");
        var body = await response.Content.ReadAsStringAsync();
        var asUser = (await response.ReadAsync<List<UserSummaryDto>>()).Single(found => found.Id == target.Id);
        var asAdmin = (await admin.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q=Kowalska")).Single(found => found.Id == target.Id);

        Assert.Null(asUser.UserName);
        Assert.Equal("Zofia Kowalska", asUser.DisplayName);
        Assert.DoesNotContain(target.UserName, body);
        Assert.Equal(target.UserName, asAdmin.UserName);
    }

    [Fact]
    public async Task Directory_FindsALoginOnlyWhenItIsTypedInFull()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("dirsearcher");
        var target = await CreateNamedUserAsync("Jan Nowak");
        var partOfLogin = target.UserName[..^3];

        var byFullLogin = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={target.UserName.ToUpperInvariant()}");
        var byPartOfLogin = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={partOfLogin}");
        var byPartOfLoginAsAdmin = await admin.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={partOfLogin}");
        var byPartOfName = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q=nowak");

        Assert.Equal(target.Id, Assert.Single(byFullLogin).Id);
        Assert.DoesNotContain(byPartOfLogin, found => found.Id == target.Id);
        Assert.Contains(byPartOfLoginAsAdmin, found => found.Id == target.Id);
        Assert.Contains(byPartOfName, found => found.Id == target.Id);
    }

    [Fact]
    public async Task Directory_ReturnsALimitedNumberOfResults()
    {
        const int limit = 20;
        var user = await _factory.CreateUserAsync("dirlimit");
        var marker = $"Crowd{Guid.NewGuid():N}"[..12];

        for (var index = 0; index < limit + 3; index++)
        {
            await CreateNamedUserAsync($"{marker} {index}");
        }

        var found = await user.GetAsync<List<UserSummaryDto>>($"{ApiRoutes.Users}?q={marker}");

        Assert.Equal(limit, found.Count);
    }

    [Fact]
    public async Task Directory_RequiresSigningIn()
    {
        var response = await _factory.Anonymous().GetAsync($"{ApiRoutes.Users}?q=admin");

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    // A user whose display name says nothing about the login, as with real people.
    private async Task<AdminUserDto> CreateNamedUserAsync(string displayName)
    {
        var admin = await _factory.AdminAsync();
        var userName = $"login{Guid.NewGuid():N}"[..20];

        return await admin.PostAsync<AdminUserDto>(ApiRoutes.AdminUsers, new CreateUserRequest
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            DisplayName = displayName,
            Password = TaskHubApiFactory.AssignedPassword
        }, HttpStatusCode.Created);
    }
}
