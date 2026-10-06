using System.Net;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Only the configured client origins may call the API from a browser.
/// </summary>
public class CorsTests : IClassFixture<TaskHubApiFactory>
{
    private const string AllowedOrigin = "http://localhost:4200";

    private readonly TaskHubApiFactory _factory;

    public CorsTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preflight_FromTheClientOrigin_IsAllowed()
    {
        var response = await PreflightAsync(AllowedOrigin, "POST");

        await response.ShouldBeAsync(HttpStatusCode.NoContent);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Theory]
    [InlineData("http://attacker.example")]
    [InlineData("https://localhost:4200")]
    [InlineData("http://localhost:4200.attacker.example")]
    [InlineData("http://localhost:42000")]
    [InlineData("http://sub.localhost:4200")]
    [InlineData("null")]
    [InlineData("file://")]
    public async Task Preflight_FromAnotherOrigin_IsNotAllowed(string origin)
    {
        var response = await PreflightAsync(origin, "POST");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("http://attacker.example")]
    [InlineData("null")]
    public async Task Response_ToAnotherOrigin_CarriesNoCorsPermission(string origin)
    {
        var user = await _factory.CreateUserAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Me);
        request.Headers.Add("Origin", origin);

        var response = await user.Client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Cors_NeverAllowsEveryOriginOrCredentials()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Providers);
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await _factory.Anonymous().Client.SendAsync(request);

        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private Task<HttpResponseMessage> PreflightAsync(string origin, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, ApiRoutes.Projects);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        return _factory.Anonymous().Client.SendAsync(request);
    }
}
