using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace TaskHub.Infrastructure.Persistence;

/// <summary>
/// Brings a database created by an earlier version up to the current model: tables and columns that are
/// missing are added, nothing that exists is changed or removed. The database is created in one go when it
/// does not exist yet, so without this a new table or column would never reach a database that already has data.
/// </summary>
public class DatabaseSchemaUpdater
{
    private const string LegacyLoginLogsTable = "LoginLogs";
    private const string ActivityLogsTable = "UserActivityLogs";

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseSchemaUpdater> _logger;

    public DatabaseSchemaUpdater(ApplicationDbContext dbContext, ILogger<DatabaseSchemaUpdater> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model;
        var relationalModel = model.GetRelationalModel();

        // Compared with nothing, the model describes itself as the operations that create it from scratch.
        var createOperations = _dbContext.GetService<IMigrationsModelDiffer>().GetDifferences(null, relationalModel);

        var pending = new List<MigrationOperation>();
        var newTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in createOperations)
        {
            if (operation is CreateTableOperation createTable)
            {
                var existingColumns = await ReadColumnsAsync(createTable.Name, createTable.Schema, cancellationToken);

                if (existingColumns is null)
                {
                    pending.Add(createTable);
                    newTables.Add(createTable.Name);
                    continue;
                }

                foreach (var column in createTable.Columns.Where(column => !existingColumns.Contains(column.Name)))
                {
                    EnsureDefaultForExistingRows(column, relationalModel);
                    pending.Add(column);
                }
            }
            else if (operation is CreateIndexOperation createIndex && newTables.Contains(createIndex.Table))
            {
                // Indexes are created only together with their table; an index over a column added later is left out.
                pending.Add(createIndex);
            }
        }

        if (pending.Count > 0)
        {
            _logger.LogInformation(
                "Updating the database schema: {Changes}",
                string.Join(", ", pending.Select(Describe)));

            var commands = _dbContext.GetService<IMigrationsSqlGenerator>().Generate(pending, model);

            await _dbContext.GetService<IMigrationCommandExecutor>()
                .ExecuteNonQueryAsync(commands, _dbContext.GetService<IRelationalConnection>(), cancellationToken);
        }

        await MoveLegacyLoginLogsAsync(cancellationToken);
    }

    // Login attempts used to have a table of their own; they now live with the rest of the activity log.
    private async Task MoveLegacyLoginLogsAsync(CancellationToken cancellationToken)
    {
        if (await ReadColumnsAsync(LegacyLoginLogsTable, null, cancellationToken) is null)
        {
            return;
        }

        var sql = _dbContext.GetService<ISqlGenerationHelper>();
        var legacy = sql.DelimitIdentifier(LegacyLoginLogsTable);
        var target = sql.DelimitIdentifier(ActivityLogsTable);

        string Column(string name)
        {
            return sql.DelimitIdentifier(name);
        }

        var copy =
            $"INSERT INTO {target} ({Column("Id")}, {Column("Type")}, {Column("UserId")}, {Column("UserName")}, {Column("Action")}, " +
            $"{Column("HttpMethod")}, {Column("Path")}, {Column("StatusCode")}, {Column("IpAddress")}, {Column("UserAgent")}, " +
            $"{Column("FailureReason")}, {Column("CreatedAt")}) " +
            $"SELECT {Column("Id")}, 'Login', {Column("UserId")}, {Column("UserName")}, 'Auth.Login', 'POST', '', " +
            $"CASE WHEN {Column("FailureReason")} IS NULL THEN 200 ELSE 401 END, {Column("IpAddress")}, {Column("UserAgent")}, " +
            $"{Column("FailureReason")}, {Column("CreatedAt")} FROM {legacy} " +
            $"WHERE {Column("Id")} NOT IN (SELECT {Column("Id")} FROM {target})";

        // Both statements are built from the fixed names above; nothing in them comes from outside.
        var drop = "DROP TABLE " + legacy;

        try
        {
            var moved = await _dbContext.Database.ExecuteSqlRawAsync(copy, cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync(drop, cancellationToken);

            _logger.LogInformation("Moved {Count} login entries into the activity log", moved);
        }
        catch (DbException exception)
        {
            // The old table stays where it is; the application works without its rows.
            _logger.LogWarning(exception, "Could not move the old login log into the activity log");
        }
    }

    /// <summary>The columns of a table, or nothing when the table does not exist.</summary>
    private async Task<HashSet<string>?> ReadColumnsAsync(string table, string? schema, CancellationToken cancellationToken)
    {
        var name = _dbContext.GetService<ISqlGenerationHelper>().DelimitIdentifier(table, schema);

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT * FROM {name} WHERE 1 = 0";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            return Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (DbException)
        {
            // Asking for a table that is not there is an error in every database; that is the answer looked for.
            return null;
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    // A required column cannot be added to a table that has rows unless those rows get a value.
    private static void EnsureDefaultForExistingRows(AddColumnOperation column, IRelationalModel relationalModel)
    {
        if (column.IsNullable || column.DefaultValue is not null || column.DefaultValueSql is not null)
        {
            return;
        }

        var property = relationalModel.FindTable(column.Table, column.Schema)?
            .FindColumn(column.Name)?
            .PropertyMappings
            .FirstOrDefault()?
            .Property;

        if (property is null)
        {
            return;
        }

        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        var value = type == typeof(string) ? string.Empty : type.IsValueType ? Activator.CreateInstance(type) : null;

        // The value is stored the way the property is, for example an enumeration as its name.
        var converter = property.GetTypeMapping().Converter;

        column.DefaultValue = converter is null ? value : converter.ConvertToProvider(value);
    }

    private static string Describe(MigrationOperation operation)
    {
        return operation switch
        {
            CreateTableOperation createTable => $"table {createTable.Name}",
            AddColumnOperation addColumn => $"column {addColumn.Table}.{addColumn.Name}",
            CreateIndexOperation createIndex => $"index {createIndex.Name}",
            _ => operation.GetType().Name
        };
    }
}
