using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Persistence.Migrations;

public static class DatabaseMigrationExtensions
{
    public static IServiceCollection AddDatabaseMigrations(this IServiceCollection services, DatabaseOptions options)
    {
        services.AddSingleton(options);
        services.AddFluentMigratorCore().ConfigureRunner(runner =>
        {
            switch (options.Provider)
            {
                case DatabaseProvider.MsSql: runner.AddSqlServer2016(); break;
                case DatabaseProvider.Postgresql: runner.AddPostgres(); break;
                case DatabaseProvider.MySql: runner.AddMySql5(); break;
                case DatabaseProvider.Sqlite: runner.AddSQLite(); break;
                default: throw new InvalidOperationException($"Unsupported database provider: {options.Provider}");
            }
            runner.WithGlobalConnectionString(options.ConnectionString)
                .ScanIn(typeof(TaskHubMigration).Assembly).For.Migrations();
        });
        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<DatabaseSchemaVerifier>();
        return services;
    }
}
