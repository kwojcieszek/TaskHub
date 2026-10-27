using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace TaskHub.Infrastructure.Persistence.Migrations;

/// <summary>
/// Checks after the migrations that every table and column of the EF Core model exists in the database,
/// so a model change without its migration stops the startup instead of failing the first request that needs it.
/// </summary>
/// <remarks>
/// Each table is read with a query that returns no rows, which works the same way on every supported provider.
/// </remarks>
public class DatabaseSchemaVerifier
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseSchemaVerifier> _logger;

    public DatabaseSchemaVerifier(ApplicationDbContext dbContext, ILogger<DatabaseSchemaVerifier> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        var model = _dbContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var sql = _dbContext.GetService<ISqlGenerationHelper>();
        var problems = new List<string>();

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            foreach (var table in model.Tables)
            {
                problems.AddRange(await FindMissingAsync(table, sql, cancellationToken));
            }
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"The database schema does not match the application model: {string.Join("; ", problems)}. " +
                "Add a migration for the model change in TaskHub.Infrastructure/Persistence/Migrations.");
        }

        _logger.LogInformation("The database schema matches the application model ({Count} tables).", model.Tables.Count());
    }

    private async Task<IEnumerable<string>> FindMissingAsync(ITable table, ISqlGenerationHelper sql, CancellationToken cancellationToken)
    {
        var tableName = sql.DelimitIdentifier(table.Name, table.Schema);
        var columns = table.Columns.Select(column => column.Name).ToList();

        if (await CanQueryAsync(string.Join(", ", columns.Select(column => sql.DelimitIdentifier(column))), tableName, cancellationToken))
        {
            return [];
        }

        // The combined query failed, so the table and then each column are tried on their own to name what is missing.
        if (!await CanQueryAsync("1", tableName, cancellationToken))
        {
            return [$"table {table.Name} is missing"];
        }

        var missing = new List<string>();

        foreach (var column in columns)
        {
            if (!await CanQueryAsync(sql.DelimitIdentifier(column), tableName, cancellationToken))
            {
                missing.Add($"column {table.Name}.{column} is missing");
            }
        }

        return missing;
    }

    private async Task<bool> CanQueryAsync(string selectList, string tableName, CancellationToken cancellationToken)
    {
        await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT {selectList} FROM {tableName} WHERE 1 = 0";

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
