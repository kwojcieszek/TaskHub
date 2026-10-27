using System.Net;
using System.Text;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Dtos.UserSettings;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Hostile and broken input: it is either stored as plain text or refused with a 4xx answer,
/// never executed, never an unhandled error.
/// </summary>
public class InputHardeningTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public InputHardeningTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string> HostileTexts => new()
    {
        "<script>alert(document.cookie)</script>",
        "<img src=x onerror=alert(1)>",
        "\"><svg/onload=alert(1)>",
        "javascript:alert(1)",
        "'; DROP TABLE Projects;--",
        "' OR '1'='1",
        "1; DELETE FROM Users",
        "Robert'); DROP TABLE Tasks;--",
        "{{7*7}}",
        "${7*7}",
        "%00",
        "../../../etc/passwd",
        "..\\..\\..\\windows\\win.ini",
        "=cmd|' /C calc'!A0",
        "\u202Eevil\u202C",
        "line one\r\nSet-Cookie: injected=1",
        "𝓤𝓷𝓲𝓬𝓸𝓭𝓮 ✓ 日本語 🚀"
    };

    [Theory]
    [MemberData(nameof(HostileTexts))]
    public async Task HostileText_IsStoredAndReturnedAsPlainData(string text)
    {
        var user = await _factory.CreateUserAsync();

        var project = await user.PostAsync<ProjectDto>(ApiRoutes.Projects, new CreateProjectRequest { Name = text, Description = text }, HttpStatusCode.Created);
        var task = await user.PostAsync<TaskItemDto>(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = text, Description = text });
        var setting = await user.PutAsync<UserSettingDto>(ApiRoutes.Setting("note"), new SetUserSettingRequest { Value = text });

        Assert.Equal(text, project.Name);
        Assert.Equal(text, project.Description);
        Assert.Equal(text, task.Title);
        Assert.Equal(text, setting.Value);

        // Read back, and as JSON, so a browser never interprets it as markup.
        var response = await user.GetAsync(ApiRoutes.Project(project.Id));
        var reloaded = await response.ReadAsync<ProjectDto>();

        Assert.Equal(text, reloaded.Name);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [MemberData(nameof(HostileTexts))]
    public async Task HostileText_InASearch_FindsNothingAndBreaksNothing(string text)
    {
        var user = await _factory.CreateUserAsync();
        var admin = await _factory.AdminAsync();
        var query = Uri.EscapeDataString(text);

        var directory = await user.GetAsync($"{ApiRoutes.Users}?q={query}");
        var loginLogs = await admin.GetAsync($"{ApiRoutes.AdminLoginLogs}?search={query}");
        var activityLogs = await admin.GetAsync($"{ApiRoutes.AdminActivityLogs}?search={query}");
        var setting = await user.GetAsync(ApiRoutes.Setting(text));

        await directory.ShouldBeAsync(HttpStatusCode.OK);
        await loginLogs.ShouldBeAsync(HttpStatusCode.OK);
        await activityLogs.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Contains(setting.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });

        // Everything is still there afterwards.
        var users = await admin.GetAsync(ApiRoutes.AdminUsers);
        var projects = await user.GetAsync(ApiRoutes.Projects);
        await users.ShouldBeAsync(HttpStatusCode.OK);
        await projects.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("page=-1&pageSize=-5")]
    [InlineData("page=0&pageSize=0")]
    [InlineData("page=2147483647&pageSize=2147483647")]
    [InlineData("pageSize=1000000")]
    public async Task AuditLogPaging_ToleratesExtremeValues(string query)
    {
        var admin = await _factory.AdminAsync();

        var response = await admin.GetAsync($"{ApiRoutes.AdminLoginLogs}?{query}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"Unexpected {(int)response.StatusCode}: {body}");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("{\"name\":")]
    [InlineData("{\"name\": 12345}")]
    [InlineData("{\"name\": {\"nested\": true}}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("")]
    public async Task MalformedJson_IsABadRequest(string body)
    {
        var user = await _factory.CreateUserAsync();
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await user.Client.PostAsync(ApiRoutes.Projects, content);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeeplyNestedJson_IsRefusedWithoutAnError()
    {
        var user = await _factory.CreateUserAsync();
        var nested = string.Concat(Enumerable.Repeat("{\"a\":", 5000)) + "1" + new string('}', 5000);
        var content = new StringContent(nested, Encoding.UTF8, "application/json");

        var response = await user.Client.PostAsync(ApiRoutes.Projects, content);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    [InlineData("application/x-www-form-urlencoded")]
    public async Task OtherContentTypes_AreNotAccepted(string contentType)
    {
        var user = await _factory.CreateUserAsync();
        var content = new StringContent("{\"name\":\"Project\"}", Encoding.UTF8, contentType);

        var response = await user.Client.PostAsync(ApiRoutes.Projects, content);

        await response.ShouldBeAsync(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task XmlBodyWithAnExternalEntity_IsNotParsed()
    {
        var user = await _factory.CreateUserAsync();
        const string xml = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY xxe SYSTEM \"file:///c:/windows/win.ini\">]><CreateProjectRequest><Name>&xxe;</Name></CreateProjectRequest>";

        var response = await user.Client.PostAsync(ApiRoutes.Projects, new StringContent(xml, Encoding.UTF8, "application/xml"));

        await response.ShouldBeAsync(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task ProjectName_IsRequiredAndLimited()
    {
        var user = await _factory.CreateUserAsync();

        var missing = await user.PostAsync(ApiRoutes.Projects, new { });
        var empty = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "" });
        var tooLong = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = new string('a', 201) });
        var longDescription = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "Project", Description = new string('a', 2001) });
        var atTheLimit = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = new string('a', 200), Description = new string('a', 2000) });

        await missing.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await empty.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await tooLong.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await longDescription.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await atTheLimit.ShouldBeAsync(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("pi-briefcase onmouseover=alert(1)")]
    [InlineData("pi-briefcase\" onclick=\"alert(1)")]
    [InlineData("fa-user")]
    [InlineData("PI-BRIEFCASE")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9ImFsZXJ0KDEpIi8+")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("data:image/png;base64,<script>alert(1)</script>")]
    [InlineData("data:image/png,not-base64")]
    [InlineData("data:image/png;base64,AAAA\" onerror=\"alert(1)")]
    [InlineData("https://attacker.example/tracker.png")]
    public async Task ProjectIcon_AcceptsOnlyAnIconNameOrAnEmbeddedImage(string icon)
    {
        var user = await _factory.CreateUserAsync();

        var create = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "Project", Icon = icon });

        await create.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var project = await user.CreateProjectAsync();
        var update = await user.PutAsync(ApiRoutes.Project(project.Id), new UpdateProjectRequest { Name = "Project", Icon = icon });

        await update.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("pi-briefcase")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("data:image/jpeg;base64,AAAA")]
    [InlineData("data:image/webp;base64,AAAA")]
    public async Task ProjectIcon_ValidValuesAreStored(string icon)
    {
        var user = await _factory.CreateUserAsync();

        var project = await user.PostAsync<ProjectDto>(ApiRoutes.Projects, new CreateProjectRequest { Name = "Project", Icon = icon }, HttpStatusCode.Created);

        Assert.Equal(icon, project.Icon);
    }

    [Fact]
    public async Task ProjectIcon_OversizedImageIsRefused()
    {
        var user = await _factory.CreateUserAsync();
        var icon = "data:image/png;base64," + new string('A', 150_000);

        var response = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "Project", Icon = icon });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/repo.git")]
    [InlineData("ssh://git@example.com/repo.git")]
    [InlineData("git@example.com:team/repo.git")]
    [InlineData("//attacker.example/repo")]
    [InlineData("/relative/path")]
    [InlineData("not a url")]
    public async Task ProjectGitUrl_AcceptsOnlyWebAddresses(string gitUrl)
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "Project", GitUrl = gitUrl });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProjectGitUrl_WebAddressIsStored_AndAnOverlongOneRefused()
    {
        var user = await _factory.CreateUserAsync();

        var valid = await user.PostAsync<ProjectDto>(ApiRoutes.Projects, new CreateProjectRequest { Name = "Project", GitUrl = "https://example.com/team/repo.git" }, HttpStatusCode.Created);
        var tooLong = await user.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "Project", GitUrl = "https://example.com/" + new string('a', 500) });

        Assert.Equal("https://example.com/team/repo.git", valid.GitUrl);
        await tooLong.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TaskFields_AreRequiredAndLimited()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();

        var missingTitle = await user.PostAsync(ApiRoutes.Tasks(project.Id), new { });
        var longTitle = await user.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = new string('a', 301) });
        var longDescription = await user.PostAsync(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "Task", Description = new string('a', 4001) });
        var unknownPriority = await user.PostAsync(ApiRoutes.Tasks(project.Id), new { title = "Task", priority = "Apocalyptic" });
        var badDate = await user.PostAsync(ApiRoutes.Tasks(project.Id), new { title = "Task", dueDate = "tomorrow" });
        var badAssignee = await user.PostAsync(ApiRoutes.Tasks(project.Id), new { title = "Task", assignedUserId = "not-a-guid" });

        await missingTitle.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await longTitle.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await longDescription.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await unknownPriority.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await badDate.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await badAssignee.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var tasks = await user.GetAsync<List<TaskItemDto>>(ApiRoutes.Tasks(project.Id));
        Assert.Empty(tasks);
    }

    [Theory]
    [InlineData("from=2026-01-01")]
    [InlineData("to=2026-01-01")]
    [InlineData("from=yesterday&to=tomorrow")]
    [InlineData("from=2026-13-45&to=2026-01-01")]
    public async Task TaskCalendar_RejectsAnIncompleteOrInvalidRange(string query)
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.GetAsync($"{ApiRoutes.AllTasks}?{query}");

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("../../evil.txt", "evil.txt")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData("folder/sub/report.pdf", "report.pdf")]
    [InlineData("  padded.txt  ", "padded.txt")]
    public async Task AttachmentFileName_IsReducedToItsLastSegment(string sentName, string storedName)
    {
        var (user, project, task) = await CreateTaskAsync();

        var attachment = await user.UploadAttachmentAsync(project.Id, task.Id, sentName, Guid.NewGuid().ToByteArray());

        Assert.Equal(storedName, attachment.FileName);
    }

    [Fact]
    public async Task AttachmentFileName_NeverDecidesWhereTheFileIsStored()
    {
        // The storage key is the content hash, so a hostile name cannot escape the attachment folder.
        var (user, project, task) = await CreateTaskAsync();
        var content = Guid.NewGuid().ToByteArray();

        var attachment = await user.UploadAttachmentAsync(project.Id, task.Id, "../../../../planted.txt", content);

        var stored = Directory.GetFiles(_factory.AttachmentsPath, "*", SearchOption.AllDirectories);
        Assert.Contains(stored, path => Path.GetFileName(path).StartsWith(attachment.Sha256, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stored, path => path.Contains("planted", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_factory.AttachmentsPath)!, "planted*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AttachmentUpload_RejectsEmptyOversizedAndNamelessFiles()
    {
        var (user, project, task) = await CreateTaskAsync();
        var url = ApiRoutes.Attachments(project.Id, task.Id);

        var empty = await user.UploadAsync(url, "empty.txt", Array.Empty<byte>());
        var tooLarge = await user.UploadAsync(url, "large.bin", new byte[TaskHubApiFactory.MaxFileSizeBytes + 1]);
        var longName = await user.UploadAsync(url, new string('a', 256) + ".txt", new byte[] { 1 });
        var atTheLimit = await user.UploadAsync(url, "limit.bin", new byte[TaskHubApiFactory.MaxFileSizeBytes]);
        var noFile = await user.Client.PostAsync(url, new MultipartFormDataContent { { new StringContent("value"), "other" } });

        await empty.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await tooLarge.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await longName.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await atTheLimit.ShouldBeAsync(HttpStatusCode.OK);
        await noFile.ShouldBeAsync(HttpStatusCode.BadRequest);

        var attachments = await user.GetAsync<List<AttachmentDto>>(url);
        Assert.Single(attachments);
    }

    [Theory]
    [InlineData("text/html", "<script>alert(document.cookie)</script>")]
    [InlineData("image/svg+xml", "<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>")]
    [InlineData("application/javascript", "alert(1)")]
    public async Task AttachmentDownload_IsAlwaysOfferedAsAFile(string contentType, string content)
    {
        // Served inline, an uploaded page or script would run in the application's origin.
        var (user, project, task) = await CreateTaskAsync();
        var attachment = await user.UploadAttachmentAsync(project.Id, task.Id, "payload.html", Encoding.UTF8.GetBytes(content + Guid.NewGuid()), contentType);

        var byId = await user.GetAsync(ApiRoutes.AttachmentContent(project.Id, task.Id, attachment.Id));
        var byHash = await user.GetAsync(ApiRoutes.AttachmentContentBySha256(attachment.Sha256, task.Id));

        foreach (var response in new[] { byId, byHash })
        {
            await response.ShouldBeAsync(HttpStatusCode.OK);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b85")]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b8555")]
    [InlineData("..%2F..%2Ftaskhub.db")]
    [InlineData("' OR '1'='1")]
    public async Task AttachmentHash_MustBeAWellFormedSha256(string sha256)
    {
        var (user, _, task) = await CreateTaskAsync();

        var find = await user.GetAsync(ApiRoutes.AttachmentsBySha256(sha256, task.Id));
        var download = await user.GetAsync(ApiRoutes.AttachmentContentBySha256(sha256, task.Id));

        await find.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await download.ReadErrorsAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AttachmentHash_RequiresTheTask()
    {
        var (user, _, _) = await CreateTaskAsync();
        const string sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        var withoutTask = await user.GetAsync($"{ApiRoutes.Root}/attachments/{sha256}");
        var withBadTask = await user.GetAsync($"{ApiRoutes.Root}/attachments/{sha256}?taskId=not-a-guid");

        await withoutTask.ShouldBeAsync(HttpStatusCode.BadRequest);
        await withBadTask.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserSetting_KeyAndValueAreLimited()
    {
        var user = await _factory.CreateUserAsync();

        var longKey = await user.PutAsync(ApiRoutes.Setting(new string('k', 101)), new SetUserSettingRequest { Value = "x" });
        var longValue = await user.PutAsync(ApiRoutes.Setting("key"), new SetUserSettingRequest { Value = new string('v', 4001) });
        var atTheLimit = await user.PutAsync(ApiRoutes.Setting(new string('k', 100)), new SetUserSettingRequest { Value = new string('v', 4000) });

        await longKey.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await longValue.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await atTheLimit.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/v1/projects/not-a-guid")]
    [InlineData("/v1/projects/123")]
    [InlineData("/v1/projects/11111111-2222-3333-4444-555555555555/tasks/not-a-guid")]
    [InlineData("/v1/project-groups/%27%20OR%201%3D1")]
    [InlineData("/v1/nothing-here")]
    [InlineData("/v2/projects")]
    [InlineData("/v0/projects")]
    [InlineData("/vX/projects")]
    [InlineData("/projects")]
    public async Task UnknownAddress_IsNotFoundOrBadRequest(string url)
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.GetAsync(url);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
    }

    [Theory]
    [InlineData("PATCH", "/v1/projects")]
    [InlineData("DELETE", "/v1/projects")]
    [InlineData("PUT", "/v1/projects")]
    [InlineData("TRACE", "/v1/projects")]
    [InlineData("DELETE", "/v1/auth/me")]
    [InlineData("PUT", "/v1/users")]
    public async Task UnsupportedMethod_IsNotAllowed(string method, string url)
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.SendAsync(new HttpMethod(method), url);

        await response.ShouldBeAsync(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task ErrorAnswers_ShareOneShapeAndRevealNoInternals()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var stranger = scenario.Cast.Stranger;
        var owner = scenario.Cast.Owner;

        var responses = new[]
        {
            await stranger.GetAsync(ApiRoutes.Project(scenario.Project.Id)),
            await owner.GetAsync(ApiRoutes.Task(scenario.Project.Id, Guid.NewGuid())),
            await owner.PostAsync(ApiRoutes.Projects, new CreateProjectRequest { Name = "" }),
            await owner.PostAsync(ApiRoutes.ProjectMembers(scenario.Project.Id), new AddProjectMemberRequest { UserId = scenario.Cast.Editor.UserId }),
            await _factory.Anonymous().PostAsync(ApiRoutes.Login, new { userName = "nobody", password = "nothing" })
        };

        foreach (var response in responses)
        {
            var body = await response.Content.ReadAsStringAsync();

            Assert.True((int)response.StatusCode is >= 400 and < 500, $"Unexpected {(int)response.StatusCode}: {body}");
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("{\"errors\":[", body);
            Assert.DoesNotContain("   at ", body);
            Assert.DoesNotContain("Exception", body);
            Assert.DoesNotContain("TaskHub.", body);
            Assert.DoesNotContain("SQLite", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<(ApiSession User, ProjectDto Project, TaskItemDto Task)> CreateTaskAsync()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();
        var task = await user.CreateTaskAsync(project.Id);

        return (user, project, task);
    }
}
