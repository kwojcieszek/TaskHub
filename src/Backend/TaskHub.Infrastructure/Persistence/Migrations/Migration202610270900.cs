using FluentMigrator;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Persistence.Migrations;

[Migration(202610270900, "Add recurrence settings to tasks")]
public sealed class Migration202610270900 : TaskHubMigration
{
    public Migration202610270900(DatabaseOptions options) : base(options)
    {
    }

    public override void Up()
    {
        Alter.Table("Tasks")
            .AddColumn("RecurrenceInterval").AsInt32().Nullable()
            .AddColumn("RecurrenceUnit").AsString(20).Nullable()
            .AddColumn("RecurrenceBasis").AsString(20).Nullable();
    }

    public override void Down()
    {
        Delete.Column("RecurrenceInterval").Column("RecurrenceUnit").Column("RecurrenceBasis").FromTable("Tasks");
    }
}
