using System.Data;
using FluentMigrator;
using FluentMigrator.SqlServer;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Persistence.Migrations;

// This is a fixed schema snapshot. Future changes belong in a new numbered migration.
[Migration(1, "Create the initial TaskHub schema")]
public sealed class MigrationInit : TaskHubMigration
{
    public MigrationInit(DatabaseOptions options) : base(options)
    {
    }

    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("NormalizedUserName").AsString(256).Nullable()
            .WithColumn("Email").AsString(256).Nullable()
            .WithColumn("NormalizedEmail").AsString(256).Nullable()
            .WithColumn("EmailConfirmed").AsBoolean().NotNullable()
            .WithColumn("PasswordHash").AsString(int.MaxValue).Nullable()
            .WithColumn("SecurityStamp").AsString(int.MaxValue).Nullable()
            .WithColumn("ConcurrencyStamp").AsString(int.MaxValue).Nullable()
            .WithColumn("PhoneNumber").AsString(int.MaxValue).Nullable()
            .WithColumn("PhoneNumberConfirmed").AsBoolean().NotNullable()
            .WithColumn("TwoFactorEnabled").AsBoolean().NotNullable()
            .WithColumn("LockoutEnd").AsCustom(OffsetType).Nullable()
            .WithColumn("LockoutEnabled").AsBoolean().NotNullable()
            .WithColumn("AccessFailedCount").AsInt32().NotNullable()
            .WithColumn("DisplayName").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("MustChangePassword").AsBoolean().NotNullable()
            .WithColumn("TokenVersion").AsInt32().NotNullable();

        Create.Table("Roles")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Name").AsString(256).Nullable()
            .WithColumn("NormalizedName").AsString(256).Nullable()
            .WithColumn("ConcurrencyStamp").AsString(int.MaxValue).Nullable();

        Create.Table("RoleClaims")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RoleId").AsGuid().NotNullable().ForeignKey("Roles", "Id").OnDelete(Rule.Cascade)
            .WithColumn("ClaimType").AsString(int.MaxValue).Nullable()
            .WithColumn("ClaimValue").AsString(int.MaxValue).Nullable();

        Create.Table("UserClaims")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("ClaimType").AsString(int.MaxValue).Nullable()
            .WithColumn("ClaimValue").AsString(int.MaxValue).Nullable();

        Create.Table("UserLogins")
            .WithColumn("LoginProvider").AsString(128).PrimaryKey("PK_UserLogins")
            .WithColumn("ProviderKey").AsString(128).PrimaryKey("PK_UserLogins")
            .WithColumn("ProviderDisplayName").AsString(int.MaxValue).Nullable()
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade);

        Create.Table("UserRoles")
            .WithColumn("UserId").AsGuid().PrimaryKey("PK_UserRoles").ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("RoleId").AsGuid().PrimaryKey("PK_UserRoles").ForeignKey("Roles", "Id").OnDelete(Rule.Cascade);

        Create.Table("UserTokens")
            .WithColumn("UserId").AsGuid().PrimaryKey("PK_UserTokens").ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("LoginProvider").AsString(128).PrimaryKey("PK_UserTokens")
            .WithColumn("Name").AsString(128).PrimaryKey("PK_UserTokens")
            .WithColumn("Value").AsString(int.MaxValue).Nullable();

        Create.Table("ProjectGroups")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("OwnerId").AsGuid().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("UpdatedAt").AsCustom(DateTimeType).Nullable();

        Create.Table("Projects")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("Icon").AsString(int.MaxValue).Nullable()
            .WithColumn("GitUrl").AsString(500).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("ProjectGroupId").AsGuid().Nullable().ForeignKey("ProjectGroups", "Id").OnDelete(Rule.SetNull)
            .WithColumn("OwnerId").AsGuid().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("UpdatedAt").AsCustom(DateTimeType).Nullable();

        Create.Table("ProjectMembers")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("ProjectId").AsGuid().NotNullable().ForeignKey("Projects", "Id").OnDelete(Rule.Cascade)
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("Role").AsInt32().NotNullable()
            .WithColumn("JoinedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("UserGroups")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(int.MaxValue).Nullable()
            .WithColumn("OwnerId").AsGuid().Nullable()
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("UserGroupMembers")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserGroupId").AsGuid().NotNullable().ForeignKey("UserGroups", "Id").OnDelete(Rule.Cascade)
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("JoinedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("ProjectTeams")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("ProjectId").AsGuid().NotNullable().ForeignKey("Projects", "Id").OnDelete(Rule.Cascade)
            .WithColumn("UserGroupId").AsGuid().NotNullable().ForeignKey("UserGroups", "Id").OnDelete(Rule.Cascade)
            .WithColumn("Role").AsInt32().NotNullable()
            .WithColumn("AddedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("Tasks")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("ProjectId").AsGuid().NotNullable().ForeignKey("Projects", "Id").OnDelete(Rule.Cascade)
            .WithColumn("ParentTaskId").AsGuid().Nullable().ForeignKey("Tasks", "Id")
            .WithColumn("Title").AsString(300).NotNullable()
            .WithColumn("Description").AsString(4000).Nullable()
            .WithColumn("DueDate").AsCustom(DateTimeType).Nullable()
            .WithColumn("Status").AsString(20).NotNullable()
            .WithColumn("Priority").AsString(20).NotNullable()
            .WithColumn("AssignedUserId").AsGuid().Nullable().ForeignKey("Users", "Id").OnDelete(Rule.SetNull)
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("UpdatedAt").AsCustom(DateTimeType).Nullable()
            .WithColumn("StartedAt").AsCustom(DateTimeType).Nullable()
            .WithColumn("CompletedAt").AsCustom(DateTimeType).Nullable();

        Create.Table("Attachments")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TaskItemId").AsGuid().NotNullable().ForeignKey("Tasks", "Id").OnDelete(Rule.Cascade)
            .WithColumn("FileName").AsString(255).NotNullable()
            .WithColumn("ContentType").AsString(255).NotNullable()
            .WithColumn("Size").AsInt64().NotNullable()
            .WithColumn("Sha256").AsFixedLengthString(64).NotNullable()
            .WithColumn("UploadedById").AsGuid().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("Comments")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("ProjectId").AsGuid().NotNullable().ForeignKey("Projects", "Id").OnDelete(Rule.Cascade)
            .WithColumn("TaskItemId").AsGuid().Nullable().ForeignKey("Tasks", "Id")
            .WithColumn("AuthorId").AsGuid().Nullable().ForeignKey("Users", "Id").OnDelete(Rule.SetNull)
            .WithColumn("Content").AsString(4000).NotNullable()
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("UpdatedAt").AsCustom(DateTimeType).Nullable();

        Create.Table("ApiTokens")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("TokenHash").AsString(64).NotNullable()
            .WithColumn("Prefix").AsString(20).NotNullable()
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("ExpiresAt").AsCustom(DateTimeType).Nullable()
            .WithColumn("LastUsedAt").AsCustom(DateTimeType).Nullable();

        Create.Table("UserSettings")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("Key").AsString(100).NotNullable()
            .WithColumn("Value").AsString(4000).NotNullable()
            .WithColumn("UpdatedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("SystemSettings")
            .WithColumn("Key").AsString(100).PrimaryKey()
            .WithColumn("Value").AsString(4000).NotNullable()
            .WithColumn("UpdatedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("UserActivityLogs")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Type").AsString(20).NotNullable()
            .WithColumn("UserId").AsGuid().Nullable().ForeignKey("Users", "Id").OnDelete(Rule.SetNull)
            .WithColumn("UserName").AsString(256).NotNullable()
            .WithColumn("Action").AsString(200).NotNullable()
            .WithColumn("HttpMethod").AsString(10).NotNullable()
            .WithColumn("Path").AsString(500).NotNullable()
            .WithColumn("StatusCode").AsInt32().NotNullable()
            .WithColumn("IpAddress").AsString(45).Nullable()
            .WithColumn("UserAgent").AsString(500).Nullable()
            .WithColumn("FailureReason").AsString(50).Nullable()
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable();

        Create.Table("UserSessions")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("TokenVersion").AsInt32().NotNullable()
            .WithColumn("CreatedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("ExpiresAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("LastUsedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("ReauthenticatedAt").AsCustom(DateTimeType).Nullable()
            .WithColumn("IpAddress").AsString(64).Nullable()
            .WithColumn("UserAgent").AsString(512).Nullable();

        Create.Table("TwoFactorChallenges")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserId").AsGuid().NotNullable().ForeignKey("Users", "Id").OnDelete(Rule.Cascade)
            .WithColumn("TokenHash").AsString(64).NotNullable()
            .WithColumn("TokenVersion").AsInt32().NotNullable()
            .WithColumn("SecurityStamp").AsString(256).NotNullable()
            .WithColumn("ExpiresAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("ConsumedAt").AsCustom(DateTimeType).Nullable();

        Create.Table("TaskChanges")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TaskItemId").AsGuid().NotNullable().ForeignKey("Tasks", "Id").OnDelete(Rule.Cascade)
            .WithColumn("ProjectId").AsGuid().NotNullable()
            .WithColumn("UserId").AsGuid().Nullable()
            .WithColumn("ActorName").AsString(256).NotNullable()
            .WithColumn("ChangedAt").AsCustom(DateTimeType).NotNullable()
            .WithColumn("Field").AsString(64).NotNullable()
            .WithColumn("OldValue").AsString(4500).Nullable()
            .WithColumn("NewValue").AsString(4500).Nullable();

        Index("Users", "UserNameIndex", true, "NormalizedUserName");
        Index("Users", "EmailIndex", false, "NormalizedEmail");
        Index("Roles", "RoleNameIndex", true, "NormalizedName");
        Index("RoleClaims", "IX_RoleClaims_RoleId", false, "RoleId");
        Index("UserClaims", "IX_UserClaims_UserId", false, "UserId");
        Index("UserLogins", "IX_UserLogins_UserId", false, "UserId");
        Index("UserRoles", "IX_UserRoles_RoleId", false, "RoleId");
        Index("ProjectGroups", "IX_ProjectGroups_OwnerId", false, "OwnerId");
        Index("Projects", "IX_Projects_OwnerId", false, "OwnerId");
        Index("Projects", "IX_Projects_ProjectGroupId", false, "ProjectGroupId");
        Index("ProjectMembers", "IX_ProjectMembers_ProjectId_UserId", true, "ProjectId", "UserId");
        Index("ProjectMembers", "IX_ProjectMembers_UserId", false, "UserId");
        Index("ProjectTeams", "IX_ProjectTeams_ProjectId_UserGroupId", true, "ProjectId", "UserGroupId");
        Index("ProjectTeams", "IX_ProjectTeams_UserGroupId", false, "UserGroupId");
        Index("UserGroupMembers", "IX_UserGroupMembers_UserGroupId_UserId", true, "UserGroupId", "UserId");
        Index("UserGroupMembers", "IX_UserGroupMembers_UserId", false, "UserId");
        Index("Tasks", "IX_Tasks_ProjectId", false, "ProjectId");
        Index("Tasks", "IX_Tasks_ParentTaskId", false, "ParentTaskId");
        Index("Tasks", "IX_Tasks_AssignedUserId", false, "AssignedUserId");
        Index("Attachments", "IX_Attachments_Sha256", false, "Sha256");
        Index("Attachments", "IX_Attachments_TaskItemId", false, "TaskItemId");
        Index("Attachments", "IX_Attachments_UploadedById", false, "UploadedById");
        Index("Comments", "IX_Comments_ProjectId_TaskItemId_CreatedAt", false, "ProjectId", "TaskItemId", "CreatedAt");
        Index("Comments", "IX_Comments_TaskItemId", false, "TaskItemId");
        Index("Comments", "IX_Comments_AuthorId", false, "AuthorId");
        Index("ApiTokens", "IX_ApiTokens_TokenHash", true, "TokenHash");
        Index("ApiTokens", "IX_ApiTokens_UserId", false, "UserId");
        Index("UserSettings", "IX_UserSettings_UserId_Key", true, "UserId", "Key");
        Index("UserActivityLogs", "IX_UserActivityLogs_Type_CreatedAt", false, "Type", "CreatedAt");
        Index("UserActivityLogs", "IX_UserActivityLogs_UserId", false, "UserId");
        Index("UserSessions", "IX_UserSessions_UserId_ExpiresAt", false, "UserId", "ExpiresAt");
        Index("TwoFactorChallenges", "IX_TwoFactorChallenges_TokenHash", true, "TokenHash");
        Index("TwoFactorChallenges", "IX_TwoFactorChallenges_UserId", false, "UserId");
        Index("TaskChanges", "IX_TaskChanges_TaskItemId_ChangedAt", false, "TaskItemId", "ChangedAt");
    }

    private void Index(string table, string name, bool unique, params string[] columns)
    {
        var index = Create.Index(name).OnTable(table).OnColumn(columns[0]).Ascending();
        foreach (var column in columns.Skip(1)) index = index.OnColumn(column).Ascending();
        if (unique)
        {
            if (Provider == DatabaseProvider.MsSql && name is "UserNameIndex" or "RoleNameIndex")
                index.WithOptions().UniqueNullsNotDistinct();
            else index.WithOptions().Unique();
        }
    }

    public override void Down()
    {
        string[] tables = [
            "TaskChanges", "TwoFactorChallenges", "UserSessions", "UserActivityLogs", "SystemSettings", "UserSettings",
            "ApiTokens", "Comments", "Attachments", "Tasks", "ProjectTeams", "UserGroupMembers", "UserGroups",
            "ProjectMembers", "Projects", "ProjectGroups", "UserTokens", "UserRoles", "UserLogins", "UserClaims", "RoleClaims", "Roles", "Users"
        ];
        foreach (var table in tables) Delete.Table(table);
    }
}
