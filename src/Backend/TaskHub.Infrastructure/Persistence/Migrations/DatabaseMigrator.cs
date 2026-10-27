using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Persistence.Migrations;

/// <summary>
/// Brings the database up to the schema of this application version at startup: every pending migration is applied,
/// after a backup of an existing SQLite database. A database already changed by a newer version is left alone.
/// </summary>
public class DatabaseMigrator
{
    private readonly IMigrationRunner _runner;
    private readonly IVersionLoader _versionLoader;
    private readonly IMigrationInformationLoader _migrationLoader;
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(
        IMigrationRunner runner,
        IVersionLoader versionLoader,
        IMigrationInformationLoader migrationLoader,
        DatabaseOptions options,
        ILogger<DatabaseMigrator> logger)
    {
        _runner = runner;
        _versionLoader = versionLoader;
        _migrationLoader = migrationLoader;
        _options = options;
        _logger = logger;
    }

    /// <summary>The path of the backup made before migrating, or null when none was needed.</summary>
    public string? BackupPath { get; private set; }

    public void Migrate()
    {
        _versionLoader.LoadVersionInfo();

        var applied = _versionLoader.VersionInfo.AppliedMigrations().ToHashSet();
        var known = _migrationLoader.LoadMigrations().Keys.Order().ToList();

        // Starting an older application on a newer schema would let it write data the schema no longer expects.
        var unknown = applied.Where(version => !known.Contains(version)).Order().ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"The database contains migrations this version of TaskHub does not know: {string.Join(", ", unknown)}. " +
                "It was updated by a newer version; deploy that version or restore a backup of the database.");
        }

        var pending = known.Where(version => !applied.Contains(version)).ToList();
        if (pending.Count == 0)
        {
            _logger.LogInformation("The database schema is up to date (version {Version}).", known.LastOrDefault());
            return;
        }

        // A new database has nothing to lose, so only one that already holds data is backed up.
        if (applied.Count > 0)
        {
            BackupSqliteDatabase(applied.Max());
        }

        _logger.LogInformation("Applying {Count} database migration(s): {Versions}.", pending.Count, string.Join(", ", pending));
        _runner.MigrateUp();
        _logger.LogInformation("The database schema was updated to version {Version}.", pending[^1]);
    }

    private void BackupSqliteDatabase(long currentVersion)
    {
        if (_options.Provider != DatabaseProvider.Sqlite)
        {
            _logger.LogWarning("Migrating the {Provider} database without an automatic backup; back it up before upgrading TaskHub.", _options.Provider);
            return;
        }

        var source = new SqliteConnectionStringBuilder(_options.ConnectionString) { Pooling = false };
        if (string.IsNullOrEmpty(source.DataSource) || source.DataSource == ":memory:" || source.Mode == SqliteOpenMode.Memory || !File.Exists(source.DataSource))
        {
            return;
        }

        var backupPath = $"{source.DataSource}.v{currentVersion}-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
        var target = new SqliteConnectionStringBuilder { DataSource = backupPath, Pooling = false };

        using (var sourceConnection = new SqliteConnection(source.ToString()))
        using (var targetConnection = new SqliteConnection(target.ToString()))
        {
            sourceConnection.Open();
            sourceConnection.BackupDatabase(targetConnection);
        }

        BackupPath = backupPath;
        _logger.LogInformation("Backed up the database to {BackupPath} before migrating.", backupPath);
    }
}
