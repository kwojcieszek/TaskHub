using System.Net;
using TaskHub.Application.Dtos.Auth;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Request limits, which are switched off for every other test. Each test starts an API instance
/// with the limiter on and limits small enough to reach.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task LoginAttempts_AreLimitedPerAddress()
    {
        const int limit = 4;

        using var factory = new TaskHubApiFactory()
            .WithSetting("RateLimiting:Enabled", "true")
            .WithSetting("RateLimiting:AuthPermitLimit", limit.ToString())
            .WithSetting("RateLimiting:PermitLimit", "1000")
            .WithSetting("Security:LockoutEnabled", "false");
        var anonymous = factory.Anonymous();

        for (var attempt = 0; attempt < limit; attempt++)
        {
            var allowed = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = "nobody", Password = "Wrong-Passw0rd-1" });
            await allowed.ShouldBeAsync(HttpStatusCode.Unauthorized);
        }

        var limited = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = "nobody", Password = "Wrong-Passw0rd-1" });

        var errors = await limited.ReadErrorsAsync(HttpStatusCode.TooManyRequests);
        Assert.Contains("Too many requests", errors[0]);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task LoginLimit_AlsoStopsTheRightPassword()
    {
        // Once the limit is reached, guessing cannot continue, whatever is sent.
        const int limit = 3;

        using var factory = new TaskHubApiFactory()
            .WithSetting("RateLimiting:Enabled", "true")
            .WithSetting("RateLimiting:AuthPermitLimit", limit.ToString())
            .WithSetting("RateLimiting:PermitLimit", "1000");
        var anonymous = factory.Anonymous();

        for (var attempt = 0; attempt < limit; attempt++)
        {
            await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = "nobody", Password = "Wrong-Passw0rd-1" });
        }

        var response = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = TaskHubApiFactory.AdminUserName, Password = TaskHubApiFactory.InitialAdminPassword });

        await response.ShouldBeAsync(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData(ApiRoutes.Register)]
    [InlineData(ApiRoutes.TwoFactorVerify)]
    [InlineData(ApiRoutes.GoogleLogin)]
    public async Task OtherSignInEndpoints_ShareTheLoginLimit(string url)
    {
        const int limit = 3;

        using var factory = new TaskHubApiFactory()
            .WithSetting("RateLimiting:Enabled", "true")
            .WithSetting("RateLimiting:AuthPermitLimit", limit.ToString())
            .WithSetting("RateLimiting:PermitLimit", "1000");
        var anonymous = factory.Anonymous();

        for (var attempt = 0; attempt < limit; attempt++)
        {
            await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = "nobody", Password = "Wrong-Passw0rd-1" });
        }

        var response = await anonymous.PostAsync(url, new { });

        await response.ShouldBeAsync(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task LoginLimit_DoesNotBlockPublicReadEndpoints()
    {
        const int limit = 2;

        using var factory = new TaskHubApiFactory()
            .WithSetting("RateLimiting:Enabled", "true")
            .WithSetting("RateLimiting:AuthPermitLimit", limit.ToString())
            .WithSetting("RateLimiting:PermitLimit", "1000");
        var anonymous = factory.Anonymous();

        for (var attempt = 0; attempt < limit + 1; attempt++)
        {
            await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = "nobody", Password = "Wrong-Passw0rd-1" });
        }

        var response = await anonymous.GetAsync(ApiRoutes.Providers);

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Requests_AreLimitedPerUser_NotAcrossUsers()
    {
        const int limit = 20;

        using var factory = new TaskHubApiFactory()
            .WithSetting("RateLimiting:Enabled", "true")
            .WithSetting("RateLimiting:AuthPermitLimit", "1000")
            .WithSetting("RateLimiting:PermitLimit", limit.ToString());
        var busy = await factory.CreateUserAsync("busy");
        var quiet = await factory.CreateUserAsync("quiet");
        var statuses = new List<HttpStatusCode>();

        for (var request = 0; request < limit + 5; request++)
        {
            var response = await busy.GetAsync(ApiRoutes.Projects);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, statuses.First());
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses.Last());
        Assert.All(statuses, status => Assert.Contains(status, new[] { HttpStatusCode.OK, HttpStatusCode.TooManyRequests }));

        // The other user has a budget of their own.
        var other = await quiet.GetAsync(ApiRoutes.Projects);
        await other.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnonymousRequests_AreLimitedToo()
    {
        const int limit = 5;

        using var factory = new TaskHubApiFactory()
            .WithSetting("RateLimiting:Enabled", "true")
            .WithSetting("RateLimiting:AuthPermitLimit", "1000")
            .WithSetting("RateLimiting:PermitLimit", limit.ToString());
        var anonymous = factory.Anonymous();
        var statuses = new List<HttpStatusCode>();

        for (var request = 0; request < limit + 3; request++)
        {
            var response = await anonymous.GetAsync(ApiRoutes.Providers);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(limit, statuses.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(3, statuses.Count(status => status == HttpStatusCode.TooManyRequests));
    }
}
