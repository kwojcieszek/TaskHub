using System.Reflection;
using FluentMigrator;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Infrastructure.Persistence.Migrations;

namespace TaskHub.Tests.Persistence;

public sealed class FluentMigrationTests : IDisposable
{
    private static readonly MigrationAttribute[] Migrations = typeof(TaskHubMigration).Assembly.GetTypes()
        .Select(type => type.GetCustomAttribute<MigrationAttribute>())
        .OfType<MigrationAttribute>()
        .ToArray();

    private static readonly long MigrationCount = Migrations.Length;
    private static readonly long LatestVersion = Migrations.Max(migration => migration.Version);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "taskhub-migrations", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    private IMigrationRunner Runner => _scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

    public FluentMigrationTests()
    {
        Directory.CreateDirectory(_directory);
        var options = new DatabaseOptions { Provider = DatabaseProvider.Sqlite, ConnectionString = $"Data Source={Path.Combine(_directory, "test.db")};Pooling=False" };
        _provider = new ServiceCollection().AddLogging().AddDatabaseMigrations(options)
            .AddDbContext<ApplicationDbContext>(builder => builder.UseSqlite(options.ConnectionString))
            .BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    [Fact]
    public async Task FreshDatabase_MatchesEfTablesColumnsKeysIndexesAndRelationships()
    {
        Runner.MigrateUp();
        await AssertSchemaMatchesModelAsync();
        Assert.Equal(MigrationCount, await ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'AspNet%' OR name = 'SchemaMigrations'"));
    }

    [Fact]
    public async Task EveryMigration_RollsBackOneByOneAndAppliesAgain()
    {
        Runner.MigrateUp();
        var versions = Migrations.Select(migration => migration.Version).OrderDescending().ToList();

        for (var index = 0; index < versions.Count; index++)
        {
            var target = index + 1 < versions.Count ? versions[index + 1] : 0;
            Runner.MigrateDown(target);
            Assert.Equal((long)(versions.Count - index - 1), await ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
        }

        Runner.MigrateUp();
        await AssertSchemaMatchesModelAsync();
    }

    [Fact]
    public async Task Migrator_BacksUpAnExistingDatabaseBeforeApplyingPendingMigrations()
    {
        var oldest = Migrations.Min(migration => migration.Version);
        Runner.MigrateUp(oldest);
        var migrator = _scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();

        migrator.Migrate();

        Assert.Equal(LatestVersion, await ScalarAsync("SELECT MAX(Version) FROM VersionInfo"));
        Assert.NotNull(migrator.BackupPath);
        await using var backup = new SqliteConnection($"Data Source={migrator.BackupPath};Pooling=False");
        await backup.OpenAsync();
        await using var command = backup.CreateCommand();
        command.CommandText = "SELECT MAX(Version) FROM VersionInfo";
        Assert.Equal(oldest, await command.ExecuteScalarAsync());
    }

    [Fact]
    public void Migrator_DoesNotBackUpAFreshDatabase()
    {
        var migrator = _scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();

        migrator.Migrate();

        Assert.Null(migrator.BackupPath);
    }

    [Fact]
    public async Task Migrator_RefusesADatabaseUpdatedByANewerVersion()
    {
        Runner.MigrateUp();
        await ExecuteAsync($"INSERT INTO VersionInfo (Version, AppliedOn, Description) VALUES ({LatestVersion + 1}, '2030-01-01', 'From the future')");

        using var nextStartup = _provider.CreateScope();
        var error = Assert.Throws<InvalidOperationException>(() => nextStartup.ServiceProvider.GetRequiredService<DatabaseMigrator>().Migrate());

        Assert.Contains((LatestVersion + 1).ToString(), error.Message);
    }

    [Fact]
    public async Task SchemaVerifier_AcceptsAFullyMigratedDatabase()
    {
        Runner.MigrateUp();

        await _scope.ServiceProvider.GetRequiredService<DatabaseSchemaVerifier>().VerifyAsync();
    }

    [Fact]
    public async Task SchemaVerifier_NamesTheColumnsOfAMissingMigration()
    {
        var latest = Migrations.OrderByDescending(migration => migration.Version).First();
        var previous = Migrations.Where(migration => migration.Version < latest.Version).Max(migration => migration.Version);
        Runner.MigrateUp(previous);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _scope.ServiceProvider.GetRequiredService<DatabaseSchemaVerifier>().VerifyAsync());

        Assert.Contains("is missing", error.Message);
        Assert.Contains("Add a migration", error.Message);
    }

    private async Task AssertSchemaMatchesModelAsync()
    {
        var model = Db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        foreach (var table in model.Tables)
        {
            await using var connection = new SqliteConnection(Db.Database.GetConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table.Name}\")";
            var columns = new HashSet<string>();
            var keys = new List<string>();
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(1));
                    if (reader.GetInt32(5) > 0) keys.Add(reader.GetString(1));
                }
            Assert.Equal(table.Columns.Select(c => c.Name).Order(), columns.Order());
            Assert.Equal(table.PrimaryKey!.Columns.Select(c => c.Name).Order(), keys.Order());
            command.CommandText = $"PRAGMA index_list(\"{table.Name}\")";
            var indexes = new Dictionary<string, bool>();
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) indexes[reader.GetString(1)] = reader.GetInt32(2) != 0;
            foreach (var index in table.Indexes)
            {
                Assert.True(indexes.ContainsKey(index.Name), $"Missing index {index.Name}");
                Assert.Equal(index.IsUnique, indexes[index.Name]);
            }
            command.CommandText = $"PRAGMA foreign_key_list(\"{table.Name}\")";
            var relationships = new HashSet<string>();
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) relationships.Add($"{reader.GetString(3)}:{reader.GetString(2)}:{reader.GetString(4)}:{reader.GetString(6)}");
            foreach (var fk in table.ForeignKeyConstraints)
            {
                var action = fk.OnDeleteAction switch
                {
                    Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade => "CASCADE",
                    Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull => "SET NULL",
                    _ => "NO ACTION"
                };
                Assert.Contains($"{fk.Columns.Single().Name}:{fk.PrincipalTable.Name}:{fk.PrincipalColumns.Single().Name}:{action}", relationships);
            }
        }
    }

    [Fact]
    public async Task RepeatedStartup_DoesNotReapplyInitialMigrationOrChangeData()
    {
        Runner.MigrateUp();
        var user = new User { Id = Guid.NewGuid(), UserName = "owner", NormalizedUserName = "OWNER", DisplayName = "Owner" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Preserved123!");
        Db.Users.Add(user);
        Db.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "Preserved project", OwnerId = user.Id });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        // Resolve a new runner scope to model the next API startup.
        using var nextStartup = _provider.CreateScope();
        nextStartup.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
        var restored = await Db.Users.SingleAsync();
        Assert.Equal(user.Id, restored.Id);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(restored, restored.PasswordHash!, "Preserved123!"));
        Assert.Equal(user.Id, (await Db.Projects.SingleAsync()).OwnerId);
        Assert.Equal(MigrationCount, await ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
        Assert.Equal(LatestVersion, await ScalarAsync("SELECT MAX(Version) FROM VersionInfo"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task ExplicitRollback_RemovesInitialSchemaAndAllowsRecreation()
    {
        Runner.MigrateUp();
        Runner.MigrateDown(0);
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT IN ('VersionInfo', 'sqlite_sequence')"));

        using var nextStartup = _provider.CreateScope();
        nextStartup.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
        Assert.Empty(await Db.Users.ToListAsync());
        Assert.Empty(await Db.Projects.ToListAsync());
        Assert.Equal(MigrationCount, await ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection(Db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection(Db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        Directory.Delete(_directory, true);
    }
}
