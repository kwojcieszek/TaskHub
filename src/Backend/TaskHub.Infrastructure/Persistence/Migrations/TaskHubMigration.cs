using FluentMigrator;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Persistence.Migrations;

/// <summary>
/// Base of every TaskHub migration: column types that differ between the database providers are named here once,
/// so each migration creates the same schema on all of them.
/// </summary>
/// <remarks>
/// A migration that has reached any database is never edited; a later change goes into a new migration.
/// Its version is the creation time as yyyyMMddHHmm, so migrations written on different branches do not collide.
/// The class and its file are named after that number (Migration202611051430 in Migration202611051430.cs);
/// what the migration changes is described in the [Migration] attribute, which also lands in the VersionInfo table.
/// </remarks>
public abstract class TaskHubMigration : Migration
{
    protected TaskHubMigration(DatabaseOptions options)
    {
        Provider = options.Provider;
    }

    protected DatabaseProvider Provider { get; }

    protected string DateTimeType => Provider switch
    {
        DatabaseProvider.MsSql => "datetime2",
        DatabaseProvider.Postgresql => "timestamp with time zone",
        DatabaseProvider.MySql => "datetime(6)",
        DatabaseProvider.Sqlite => "DATETIME",
        _ => throw new InvalidOperationException($"Unsupported database provider: {Provider}")
    };

    protected string OffsetType => Provider == DatabaseProvider.MsSql ? "datetimeoffset" : DateTimeType;
}
