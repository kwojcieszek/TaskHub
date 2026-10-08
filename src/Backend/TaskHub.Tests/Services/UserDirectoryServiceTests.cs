using TaskHub.Application.Common;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class UserDirectoryServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IUserDirectoryService _directory;

    public UserDirectoryServiceTests()
    {
        _directory = _host.Get<IUserDirectoryService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    [InlineData(" a ")]
    public async Task Search_ReturnsNothingForAMissingOrTooShortQuery(string? query)
    {
        var searcher = await ArrangeAsync();
        _host.SignIn(searcher, Roles.Admin);

        Assert.Empty(await _directory.SearchAsync(query));
    }

    [Fact]
    public async Task Search_ReturnsNothingForAnOverlongQuery()
    {
        var searcher = await ArrangeAsync();
        _host.SignIn(searcher);

        Assert.Empty(await _directory.SearchAsync(new string('a', 101)));
    }

    [Theory]
    [InlineData("Kowalska")]
    [InlineData("kowalska")]
    [InlineData("KOWAL")]
    [InlineData("  Zofia Kow  ")]
    public async Task Search_FindsByPartOfTheDisplayName_WhateverTheCase(string query)
    {
        var searcher = await ArrangeAsync();
        _host.SignIn(searcher);

        var found = Assert.Single(await _directory.SearchAsync(query));

        Assert.Equal("Zofia Kowalska", found.DisplayName);
    }

    [Fact]
    public async Task Search_FindsALoginOnlyWhenItIsComplete_UnlessAnAdministratorAsks()
    {
        var searcher = await ArrangeAsync();

        _host.SignIn(searcher);
        var byFullLogin = await _directory.SearchAsync("ZKOWALSKA");
        var byPartOfLogin = await _directory.SearchAsync("zkowal");

        _host.SignIn(searcher, Roles.Admin);
        var byPartOfLoginAsAdmin = await _directory.SearchAsync("zkowal");

        Assert.Equal("Zofia Kowalska", Assert.Single(byFullLogin).DisplayName);
        Assert.Empty(byPartOfLogin);
        Assert.Equal("Zofia Kowalska", Assert.Single(byPartOfLoginAsAdmin).DisplayName);
    }

    [Fact]
    public async Task Search_ShowsLoginsToAdministratorsOnly()
    {
        var searcher = await ArrangeAsync();

        _host.SignIn(searcher);
        var asUser = Assert.Single(await _directory.SearchAsync("Kowalska"));

        _host.SignIn(searcher, Roles.Admin);
        var asAdmin = Assert.Single(await _directory.SearchAsync("Kowalska"));

        Assert.Null(asUser.UserName);
        Assert.Equal("zkowalska", asAdmin.UserName);
        Assert.Equal(asUser.Id, asAdmin.Id);
    }

    [Theory]
    [InlineData("%%")]
    [InlineData("__")]
    [InlineData("%a%")]
    public async Task Search_TreatsWildcardsAsOrdinaryText(string query)
    {
        var searcher = await ArrangeAsync();
        _host.SignIn(searcher, Roles.Admin);

        Assert.Empty(await _directory.SearchAsync(query));
    }

    [Fact]
    public async Task Search_ReturnsAtMostTwentyUsers_InNameOrder()
    {
        var searcher = await ArrangeAsync();

        for (var index = 0; index < 25; index++)
        {
            await CreateNamedUserAsync($"crowd{index:00}", $"Crowd {index:00}");
        }

        _host.SignIn(searcher);
        var found = await _directory.SearchAsync("Crowd");

        Assert.Equal(20, found.Count);
        Assert.Equal(found.Select(user => user.DisplayName).OrderBy(name => name, StringComparer.Ordinal), found.Select(user => user.DisplayName));
    }

    private async Task<User> ArrangeAsync()
    {
        await CreateNamedUserAsync("zkowalska", "Zofia Kowalska");
        await CreateNamedUserAsync("jnowak", "Jan Nowak");

        return await CreateNamedUserAsync("searcher", "Searching Person");
    }

    private async Task<User> CreateNamedUserAsync(string userName, string displayName)
    {
        var user = await _host.CreateUserAsync(userName);
        user.DisplayName = displayName;
        await _host.Users.UpdateAsync(user);

        return user;
    }
}
