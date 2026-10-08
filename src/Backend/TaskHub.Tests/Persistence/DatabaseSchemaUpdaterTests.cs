using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskHub.Application.Dtos.ApiTokens;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Persistence;

/// <summary>
/// A database made by an earlier version is brought up to the current model without losing what is in it.
/// </summary>
public class DatabaseSchemaUpdaterTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly DatabaseSchemaUpdater _updater;

    public DatabaseSchemaUpdaterTests()
    {
        _updater = new DatabaseSchemaUpdater(_host.Db, NullLogger<DatabaseSchemaUpdater>.Instance);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task UpToDateDatabase_IsLeftAsItIs()
    {
        var before = await ReadSchemaAsync();

        await _updater.UpdateAsync();

        Assert.Equal(before, await ReadSchemaAsync());
    }

    [Fact]
    public async Task MissingTablesAndColumns_AreAdded_AndExistingRowsKept()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner, "Kept project");
        await _host.Get<IAuditLogService>().RecordActivityAsync(owner.Id, "owner", "Projects.Create", "POST", "/api/v1/projects", 201);
        await DowngradeAsync();

        await _updater.UpdateAsync();
        _host.Db.ChangeTracker.Clear();

        var tables = await ReadTablesAsync();
        var activity = await _host.Db.UserActivityLogs.AsNoTracking().SingleAsync(entry => entry.Action == "Projects.Create");

        Assert.Contains("ApiTokens", tables);
        Assert.Contains("Comments", tables);
        Assert.Contains("ProjectTeams", tables);
        Assert.Equal(UserActivityType.Request, activity.Type);
        Assert.Null(activity.FailureReason);
        Assert.Equal("Kept project", (await _host.Get<IProjectService>().GetByIdAsync(project.Id)).Name);
    }

    [Fact]
    public async Task UpdatedDatabase_ServesTheFeaturesThatNeededIt()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var project = await _host.CreateProjectAsync(owner);
        var team = await _host.CreateTeamAsync("Developers", member);
        await DowngradeAsync();

        await _updater.UpdateAsync();
        _host.Db.ChangeTracker.Clear();
        _host.SignIn(owner);

        var comment = await _host.Get<ICommentService>().AddToProjectAsync(project.Id, new SaveCommentRequest { Content = "Works" });
        var token = await _host.Get<IApiTokenService>().CreateAsync(new CreateApiTokenRequest { Name = "Works" });
        var added = await _host.Get<IProjectTeamService>().AddAsync(project.Id, new AddProjectTeamRequest { UserGroupId = team.Id, Role = ProjectMemberRole.Viewer });
        await _host.Get<IAuditLogService>().RecordLoginAsync(owner.Id, "owner", null);

        Assert.Equal("Works", comment.Content);
        Assert.NotNull(await _host.Get<IApiTokenService>().AuthenticateAsync(token.Token));
        Assert.Equal("Developers", added.Name);
        Assert.Equal(1, await _host.Db.UserActivityLogs.CountAsync(entry => entry.Type == UserActivityType.Login));
    }

    [Fact]
    public async Task OldLoginLog_IsMovedIntoTheActivityLog()
    {
        var owner = await _host.CreateUserAsync("owner");
        await DowngradeAsync();
        var succeeded = Guid.NewGuid();
        var failed = Guid.NewGuid();
        await InsertLegacyLoginAsync(succeeded, owner.Id, "owner", null);
        await InsertLegacyLoginAsync(failed, null, "nobody", "UserNotFound");

        await _updater.UpdateAsync();
        _host.Db.ChangeTracker.Clear();

        var logins = await _host.Db.UserActivityLogs.AsNoTracking().Where(entry => entry.Type == UserActivityType.Login).ToListAsync();
        var first = logins.Single(entry => entry.Id == succeeded);
        var second = logins.Single(entry => entry.Id == failed);

        Assert.Equal(2, logins.Count);
        Assert.Equal(owner.Id, first.UserId);
        Assert.Null(first.FailureReason);
        Assert.Equal(200, first.StatusCode);
        Assert.Equal("legacy-agent", first.UserAgent);
        Assert.Null(second.UserId);
        Assert.Equal(LoginFailureReason.UserNotFound, second.FailureReason);
        Assert.Equal(401, second.StatusCode);
        Assert.DoesNotContain("LoginLogs", await ReadTablesAsync());

        var history = await _host.Get<IAuditLogService>().GetLoginLogsForUserAsync(owner.Id, new Application.Dtos.Audit.AuditLogQuery());
        Assert.Equal(succeeded, Assert.Single(history.Items).Id);
    }

    [Fact]
    public async Task RunningTwice_ChangesNothingTheSecondTime()
    {
        await DowngradeAsync();
        await InsertLegacyLoginAsync(Guid.NewGuid(), null, "nobody", "UserNotFound");

        await _updater.UpdateAsync();
        var afterFirst = await ReadSchemaAsync();
        await _updater.UpdateAsync();

        Assert.Equal(afterFirst, await ReadSchemaAsync());
        Assert.Equal(1, await _host.Db.UserActivityLogs.CountAsync());
    }

    // Takes the database back to how it looked before tokens, teams, comments and the merged login log.
    private async Task DowngradeAsync()
    {
        var statements = new[]
        {
            "DROP TABLE \"Comments\"",
            "DROP TABLE \"ApiTokens\"",
            "DROP TABLE \"ProjectTeams\"",
            "DROP INDEX \"IX_UserActivityLogs_Type_CreatedAt\"",
            "ALTER TABLE \"UserActivityLogs\" DROP COLUMN \"Type\"",
            "ALTER TABLE \"UserActivityLogs\" DROP COLUMN \"UserAgent\"",
            "ALTER TABLE \"UserActivityLogs\" DROP COLUMN \"FailureReason\"",
            "CREATE TABLE \"LoginLogs\" (\"Id\" TEXT NOT NULL PRIMARY KEY, \"UserId\" TEXT NULL, \"UserName\" TEXT NOT NULL, \"IpAddress\" TEXT NULL, " +
            "\"UserAgent\" TEXT NULL, \"Succeeded\" INTEGER NOT NULL, \"FailureReason\" TEXT NULL, \"CreatedAt\" TEXT NOT NULL)"
        };

        foreach (var statement in statements)
        {
            await _host.Db.Database.ExecuteSqlRawAsync(statement);
        }

        _host.Db.ChangeTracker.Clear();
    }

    private async Task InsertLegacyLoginAsync(Guid id, Guid? userId, string userName, string? failureReason)
    {
        var connection = (SqliteConnection)_host.Db.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO \"LoginLogs\" (\"Id\", \"UserId\", \"UserName\", \"IpAddress\", \"UserAgent\", \"Succeeded\", \"FailureReason\", \"CreatedAt\") " +
            "VALUES ($id, $userId, $userName, '10.0.0.1', 'legacy-agent', $succeeded, $failureReason, '2026-01-01 10:00:00')";

        command.Parameters.AddWithValue("$id", id.ToString().ToUpperInvariant());
        command.Parameters.AddWithValue("$userId", userId.HasValue ? userId.Value.ToString().ToUpperInvariant() : DBNull.Value);
        command.Parameters.AddWithValue("$userName", userName);
        command.Parameters.AddWithValue("$succeeded", failureReason is null ? 1 : 0);
        command.Parameters.AddWithValue("$failureReason", failureReason is null ? DBNull.Value : failureReason);

        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> ReadTablesAsync()
    {
        return (await ReadSchemaAsync())
            .Where(entry => entry.StartsWith("table:", StringComparison.Ordinal))
            .Select(entry => entry["table:".Length..])
            .ToList();
    }

    private async Task<List<string>> ReadSchemaAsync()
    {
        var connection = (SqliteConnection)_host.Db.Database.GetDbConnection();
        var entries = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, name FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type, name";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            entries.Add($"{reader.GetString(0)}:{reader.GetString(1)}");
        }

        return entries;
    }
}
