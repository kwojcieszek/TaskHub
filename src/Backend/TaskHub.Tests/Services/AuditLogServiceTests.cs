using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class AuditLogServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IAuditLogService _audit;

    public AuditLogServiceTests()
    {
        _audit = _host.Get<IAuditLogService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task SuccessfulLogin_IsRecordedAsALoginEntryWithTheCallersAddress()
    {
        var user = await _host.CreateUserAsync("anna");

        await _audit.RecordLoginAsync(user.Id, "anna", null);
        var entry = await _host.Db.UserActivityLogs.AsNoTracking().SingleAsync();

        Assert.Equal(UserActivityType.Login, entry.Type);
        Assert.Equal(user.Id, entry.UserId);
        Assert.Equal("anna", entry.UserName);
        Assert.Null(entry.FailureReason);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal("POST", entry.HttpMethod);
        Assert.Equal("127.0.0.1", entry.IpAddress);
        Assert.Equal("tests", entry.UserAgent);
    }

    [Fact]
    public async Task FailedLogin_KeepsTheReason_AlsoForAnUnknownUser()
    {
        await _audit.RecordLoginAsync(null, "nobody", LoginFailureReason.UserNotFound);
        var entry = await _host.Db.UserActivityLogs.AsNoTracking().SingleAsync();

        Assert.Equal(UserActivityType.Login, entry.Type);
        Assert.Null(entry.UserId);
        Assert.Equal(LoginFailureReason.UserNotFound, entry.FailureReason);
        Assert.Equal(401, entry.StatusCode);
    }

    [Fact]
    public async Task Activity_IsRecordedAsARequestEntry()
    {
        var user = await _host.CreateUserAsync("anna");

        await _audit.RecordActivityAsync(user.Id, "anna", "Projects.Create", "POST", "/api/v1/projects", 201);
        var entry = await _host.Db.UserActivityLogs.AsNoTracking().SingleAsync();

        Assert.Equal(UserActivityType.Request, entry.Type);
        Assert.Equal("Projects.Create", entry.Action);
        Assert.Equal("/api/v1/projects", entry.Path);
        Assert.Equal(201, entry.StatusCode);
        Assert.Null(entry.FailureReason);
        Assert.Null(entry.UserAgent);
    }

    [Fact]
    public async Task OverlongValues_AreCutToWhatTheDatabaseHolds()
    {
        var user = await _host.CreateUserAsync("anna");

        await _audit.RecordLoginAsync(null, new string('u', 400), LoginFailureReason.UserNotFound);
        await _audit.RecordActivityAsync(user.Id, new string('u', 400), new string('a', 400), "POST", new string('p', 900), 200);
        var entries = await _host.Db.UserActivityLogs.AsNoTracking().ToListAsync();

        Assert.All(entries, entry => Assert.Equal(256, entry.UserName.Length));
        Assert.Contains(entries, entry => entry.Action.Length == 200 && entry.Path.Length == 500);
    }

    [Fact]
    public async Task Logins_AndActivity_AreReadSeparately()
    {
        var user = await _host.CreateUserAsync("anna");
        await _audit.RecordLoginAsync(user.Id, "anna", null);
        await _audit.RecordLoginAsync(user.Id, "anna", LoginFailureReason.InvalidPassword);
        await _audit.RecordActivityAsync(user.Id, "anna", "Projects.Create", "POST", "/api/v1/projects", 201);

        var logins = await _audit.GetLoginLogsAsync(new AuditLogQuery());
        var activity = await _audit.GetActivityLogsAsync(new AuditLogQuery());

        Assert.Equal(2, logins.TotalCount);
        Assert.All(logins.Items, entry => Assert.Equal(UserActivityType.Login, entry.Type));
        Assert.Equal(1, activity.TotalCount);
        Assert.Equal(UserActivityType.Request, Assert.Single(activity.Items).Type);
        Assert.All(logins.Items, entry => Assert.Equal(DateTimeKind.Utc, entry.CreatedAt.Kind));
    }

    [Fact]
    public async Task OwnLogins_ContainOnlyTheGivenUsers()
    {
        var anna = await _host.CreateUserAsync("anna");
        var bartek = await _host.CreateUserAsync("bartek");
        await _audit.RecordLoginAsync(anna.Id, "anna", null);
        await _audit.RecordLoginAsync(bartek.Id, "bartek", null);
        await _audit.RecordLoginAsync(null, "anna", LoginFailureReason.UserNotFound);
        await _audit.RecordActivityAsync(anna.Id, "anna", "Projects.Create", "POST", "/api/v1/projects", 201);

        var mine = await _audit.GetLoginLogsForUserAsync(anna.Id, new AuditLogQuery { Search = "bartek" });
        var all = await _audit.GetLoginLogsForUserAsync(anna.Id, new AuditLogQuery());

        Assert.Empty(mine.Items);
        Assert.Equal(anna.Id, Assert.Single(all.Items).UserId);
    }

    [Fact]
    public async Task Search_MatchesTheUserNameAndForActivityAlsoTheActionAndThePath()
    {
        var user = await _host.CreateUserAsync("anna");
        await _audit.RecordLoginAsync(user.Id, "anna", null);
        await _audit.RecordLoginAsync(null, "bartek", LoginFailureReason.UserNotFound);
        await _audit.RecordActivityAsync(user.Id, "anna", "Projects.Create", "POST", "/api/v1/projects", 201);
        await _audit.RecordActivityAsync(user.Id, "anna", "Tasks.Delete", "DELETE", "/api/v1/projects/1/tasks/2", 204);

        var loginsByName = await _audit.GetLoginLogsAsync(new AuditLogQuery { Search = " bart " });
        var loginsByAddress = await _audit.GetLoginLogsAsync(new AuditLogQuery { Search = "127.0" });
        var activityByAction = await _audit.GetActivityLogsAsync(new AuditLogQuery { Search = "Tasks.Delete" });
        var activityByPath = await _audit.GetActivityLogsAsync(new AuditLogQuery { Search = "/tasks/" });
        var wildcard = await _audit.GetActivityLogsAsync(new AuditLogQuery { Search = "%" });

        Assert.Equal("bartek", Assert.Single(loginsByName.Items).UserName);
        Assert.Equal(2, loginsByAddress.TotalCount);
        Assert.Single(activityByAction.Items);
        Assert.Single(activityByPath.Items);
        Assert.Empty(wildcard.Items);
    }

    [Fact]
    public async Task Paging_ReturnsNewestFirst_AndKeepsThePageWithinLimits()
    {
        var user = await _host.CreateUserAsync("anna");

        for (var index = 0; index < 5; index++)
        {
            await _audit.RecordActivityAsync(user.Id, "anna", $"Action{index}", "POST", "/path", 200);
        }

        var firstPage = await _audit.GetActivityLogsAsync(new AuditLogQuery { Page = 1, PageSize = 2 });
        var lastPage = await _audit.GetActivityLogsAsync(new AuditLogQuery { Page = 3, PageSize = 2 });
        var beyond = await _audit.GetActivityLogsAsync(new AuditLogQuery { Page = 99, PageSize = 2 });
        var corrected = await _audit.GetActivityLogsAsync(new AuditLogQuery { Page = -5, PageSize = 0 });
        var huge = await _audit.GetActivityLogsAsync(new AuditLogQuery { Page = 1, PageSize = 100000 });

        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(lastPage.Items);
        Assert.Empty(beyond.Items);
        Assert.Equal(1, corrected.Page);
        Assert.Equal(1, corrected.PageSize);
        Assert.Equal(200, huge.PageSize);
        Assert.Equal(5, huge.Items.Count);
        Assert.Equal(huge.Items.Select(entry => entry.CreatedAt).OrderByDescending(time => time), huge.Items.Select(entry => entry.CreatedAt));
    }
}
