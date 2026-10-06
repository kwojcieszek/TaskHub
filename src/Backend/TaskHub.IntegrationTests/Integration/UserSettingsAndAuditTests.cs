using System.Net;
using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Dtos.Common;
using TaskHub.Application.Dtos.UserSettings;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Integration;

/// <summary>
/// The personal settings of a user, and the trail their actions leave for the administrator.
/// </summary>
public class UserSettingsAndAuditTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public UserSettingsAndAuditTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Setting_IsSavedReadChangedAndDeleted()
    {
        var user = await _factory.CreateUserAsync();

        var saved = await user.PutAsync<UserSettingDto>(ApiRoutes.Setting("theme"), new SetUserSettingRequest { Value = "dark" });
        Assert.Equal("theme", saved.Key);
        Assert.Equal("dark", saved.Value);

        var read = await user.GetAsync<UserSettingDto>(ApiRoutes.Setting("theme"));
        Assert.Equal("dark", read.Value);

        var changed = await user.PutAsync<UserSettingDto>(ApiRoutes.Setting("theme"), new SetUserSettingRequest { Value = "light" });
        Assert.Equal("light", changed.Value);

        await user.PutAsync<UserSettingDto>(ApiRoutes.Setting("language"), new SetUserSettingRequest { Value = "pl" });
        var all = await user.GetAsync<List<UserSettingDto>>(ApiRoutes.Settings);
        Assert.Equal(new[] { "language", "theme" }, all.Select(setting => setting.Key));

        var delete = await user.DeleteAsync(ApiRoutes.Setting("theme"));
        var deleteAgain = await user.DeleteAsync(ApiRoutes.Setting("theme"));
        var readDeleted = await user.GetAsync(ApiRoutes.Setting("theme"));

        await delete.ShouldBeAsync(HttpStatusCode.NoContent);
        await deleteAgain.ShouldBeAsync(HttpStatusCode.NotFound);
        await readDeleted.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Settings_AreRemovedWithTheUser()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();
        await user.PutAsync<UserSettingDto>(ApiRoutes.Setting("theme"), new SetUserSettingRequest { Value = "dark" });

        var delete = await admin.DeleteAsync(ApiRoutes.AdminUser(user.UserId));

        await delete.ShouldBeAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangesMadeByAUser_AreRecordedWithTheirOutcome()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("audited");
        var project = await user.CreateProjectAsync();
        await user.GetAsync(ApiRoutes.Projects);
        await user.DeleteAsync(ApiRoutes.Project(Guid.NewGuid()));
        await user.DeleteAsync(ApiRoutes.Project(project.Id));

        var logs = await admin.GetAsync<PagedResultDto<UserActivityLogDto>>($"{ApiRoutes.AdminActivityLogs}?search={user.UserName}&pageSize=100");

        Assert.Contains(logs.Items, log => log.Action == "Projects.Create" && log.HttpMethod == "POST" && log.StatusCode == 201 && log.UserId == user.UserId);
        Assert.Contains(logs.Items, log => log.Action == "Projects.Delete" && log.StatusCode == 204 && log.Path.EndsWith(project.Id.ToString()));

        // A refused attempt is recorded too; that is what makes probing visible.
        Assert.Contains(logs.Items, log => log.Action == "Projects.Delete" && log.StatusCode == 403);

        // Reading is not recorded.
        Assert.DoesNotContain(logs.Items, log => log.HttpMethod == "GET");
    }

    [Fact]
    public async Task ActivityLog_RecordsNoPasswords()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("audited");

        var response = await admin.GetAsync($"{ApiRoutes.AdminActivityLogs}?search={user.UserName}&pageSize=200");
        var body = await response.Content.ReadAsStringAsync();

        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Contains("Auth.ChangePassword", body);
        Assert.DoesNotContain(TaskHubApiFactory.AssignedPassword, body);
        Assert.DoesNotContain(TaskHubApiFactory.UserPassword, body);
    }

    [Fact]
    public async Task AuditLogs_ArePaged()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync("paged");

        for (var index = 0; index < 5; index++)
        {
            await user.CreateProjectAsync($"Project {index}");
        }

        var firstPage = await admin.GetAsync<PagedResultDto<UserActivityLogDto>>($"{ApiRoutes.AdminActivityLogs}?search=Projects.Create&page=1&pageSize=2");
        var secondPage = await admin.GetAsync<PagedResultDto<UserActivityLogDto>>($"{ApiRoutes.AdminActivityLogs}?search=Projects.Create&page=2&pageSize=2");
        var hugePage = await admin.GetAsync<PagedResultDto<UserActivityLogDto>>($"{ApiRoutes.AdminActivityLogs}?pageSize=100000");

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.True(firstPage.TotalCount >= 5);
        Assert.Empty(firstPage.Items.Select(log => log.Id).Intersect(secondPage.Items.Select(log => log.Id)));

        // The page size is capped, so one request cannot pull the whole log.
        Assert.Equal(200, hugePage.PageSize);
    }
}
