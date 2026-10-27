using System.Net;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Without signing in, only the sign-in endpoints themselves answer.
/// </summary>
public class AnonymousAccessTests : IClassFixture<TaskHubApiFactory>
{
    private const string Id = "11111111-2222-3333-4444-555555555555";
    private const string OtherId = "66666666-7777-8888-9999-000000000000";
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private readonly TaskHubApiFactory _factory;

    public AnonymousAccessTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "GET", "/v1/auth/me" },
        { "GET", "/v1/auth/logins" },
        { "POST", "/v1/auth/password" },
        { "POST", "/v1/auth/2fa/setup" },
        { "POST", "/v1/auth/2fa/enable" },
        { "POST", "/v1/auth/2fa/disable" },
        { "POST", "/v1/auth/google/link" },
        { "DELETE", "/v1/auth/google/link" },

        { "GET", "/v1/admin/users" },
        { "POST", "/v1/admin/users" },
        { "PUT", $"/v1/admin/users/{Id}/password" },
        { "PUT", $"/v1/admin/users/{Id}/roles" },
        { "PUT", $"/v1/admin/users/{Id}/lockout" },
        { "DELETE", $"/v1/admin/users/{Id}" },
        { "GET", "/v1/admin/logs/logins" },
        { "GET", "/v1/admin/logs/activity" },
        { "GET", "/v1/admin/security" },
        { "PUT", "/v1/admin/security" },

        { "GET", "/v1/user-groups" },
        { "POST", "/v1/user-groups" },
        { "PUT", $"/v1/user-groups/{Id}" },
        { "DELETE", $"/v1/user-groups/{Id}" },
        { "GET", $"/v1/user-groups/{Id}/members" },
        { "POST", $"/v1/user-groups/{Id}/members" },
        { "DELETE", $"/v1/user-groups/{Id}/members/{OtherId}" },

        { "GET", "/v1/users" },
        { "GET", "/v1/users?q=admin" },

        { "GET", "/v1/settings" },
        { "GET", "/v1/settings/theme" },
        { "PUT", "/v1/settings/theme" },
        { "DELETE", "/v1/settings/theme" },

        { "GET", "/v1/projects" },
        { "POST", "/v1/projects" },
        { "GET", $"/v1/projects/{Id}" },
        { "PUT", $"/v1/projects/{Id}" },
        { "DELETE", $"/v1/projects/{Id}" },
        { "PUT", $"/v1/projects/{Id}/group" },
        { "GET", $"/v1/projects/{Id}/members" },
        { "POST", $"/v1/projects/{Id}/members" },
        { "DELETE", $"/v1/projects/{Id}/members/{OtherId}" },

        { "GET", "/v1/project-groups" },
        { "POST", "/v1/project-groups" },
        { "GET", $"/v1/project-groups/{Id}" },
        { "PUT", $"/v1/project-groups/{Id}" },
        { "DELETE", $"/v1/project-groups/{Id}" },
        { "GET", $"/v1/project-groups/{Id}/projects" },

        { "GET", "/v1/tasks" },
        { "GET", "/v1/tasks?from=2026-01-01&to=2026-12-31" },
        { "GET", $"/v1/projects/{Id}/tasks" },
        { "POST", $"/v1/projects/{Id}/tasks" },
        { "GET", $"/v1/projects/{Id}/tasks/{OtherId}" },
        { "PUT", $"/v1/projects/{Id}/tasks/{OtherId}" },
        { "DELETE", $"/v1/projects/{Id}/tasks/{OtherId}" },
        { "GET", $"/v1/projects/{Id}/tasks/{OtherId}/subtasks" },
        { "POST", $"/v1/projects/{Id}/tasks/{OtherId}/subtasks" },
        { "POST", $"/v1/projects/{Id}/tasks/{OtherId}/start" },
        { "POST", $"/v1/projects/{Id}/tasks/{OtherId}/complete" },

        { "GET", $"/v1/projects/{Id}/tasks/{OtherId}/attachments" },
        { "GET", $"/v1/projects/{Id}/tasks/{OtherId}/attachments/{Id}/content" },
        { "DELETE", $"/v1/projects/{Id}/tasks/{OtherId}/attachments/{Id}" },
        { "GET", "/v1/attachments/limits" },
        { "GET", $"/v1/attachments/{Sha256}?taskId={Id}" },
        { "GET", $"/v1/attachments/{Sha256}/content?taskId={Id}" },

        { "GET", "/v1/tokens" },
        { "POST", "/v1/tokens" },
        { "DELETE", $"/v1/tokens/{Id}" },

        { "GET", $"/v1/projects/{Id}/teams" },
        { "GET", $"/v1/projects/{Id}/teams/available" },
        { "POST", $"/v1/projects/{Id}/teams" },
        { "DELETE", $"/v1/projects/{Id}/teams/{OtherId}" },

        { "GET", $"/v1/projects/{Id}/comments" },
        { "POST", $"/v1/projects/{Id}/comments" },
        { "GET", $"/v1/projects/{Id}/tasks/{OtherId}/comments" },
        { "POST", $"/v1/projects/{Id}/tasks/{OtherId}/comments" },
        { "PUT", $"/v1/projects/{Id}/comments/{OtherId}" },
        { "DELETE", $"/v1/projects/{Id}/comments/{OtherId}" }
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_RequiresSigningIn(string method, string url)
    {
        var anonymous = _factory.Anonymous();
        var hasBody = method is "POST" or "PUT";

        var response = await anonymous.SendAsync(new HttpMethod(method), url, hasBody ? new { } : null);

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
    }

    [Fact]
    public async Task Upload_RequiresSigningIn()
    {
        // The upload only accepts a multipart form, so it is tried with one rather than with JSON.
        var url = ApiRoutes.Attachments(Guid.Parse(Id), Guid.Parse(OtherId));

        var response = await _factory.Anonymous().UploadAsync(url, "file.txt", new byte[] { 1, 2, 3 });

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_AnswersNothingToAnAnonymousCaller()
    {
        var response = await _factory.Anonymous().GetAsync(ApiRoutes.AdminUsers);
        var body = await response.Content.ReadAsStringAsync();

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public async Task Providers_IsTheOnlyPublicReadEndpoint()
    {
        var response = await _factory.Anonymous().GetAsync(ApiRoutes.Providers);

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiDocumentation_IsNotServedOutsideDevelopment()
    {
        var anonymous = _factory.Anonymous();

        var openApi = await anonymous.GetAsync("/openapi/v1.json");
        var scalar = await anonymous.GetAsync("/scalar/v1");

        await openApi.ShouldBeAsync(HttpStatusCode.NotFound);
        await scalar.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/taskhub.db")]
    [InlineData("/appsettings.json")]
    [InlineData("/attachments/")]
    [InlineData("/.env")]
    [InlineData("/v1/../appsettings.json")]
    public async Task ServerFiles_AreNotServed(string url)
    {
        var response = await _factory.Anonymous().GetAsync(url);

        await response.ShouldBeAsync(HttpStatusCode.NotFound);
    }
}
