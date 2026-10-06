namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Addresses of the API, kept in one place so a test reads as the operation it performs.
/// </summary>
public static class ApiRoutes
{
    public const string Root = "/api/v1";

    public const string Login = Root + "/auth/login";
    public const string Register = Root + "/auth/register";
    public const string Me = Root + "/auth/me";
    public const string Password = Root + "/auth/password";
    public const string Providers = Root + "/auth/providers";
    public const string MyLogins = Root + "/auth/logins";
    public const string TwoFactorSetup = Root + "/auth/2fa/setup";
    public const string TwoFactorEnable = Root + "/auth/2fa/enable";
    public const string TwoFactorDisable = Root + "/auth/2fa/disable";
    public const string TwoFactorVerify = Root + "/auth/2fa/verify";
    public const string GoogleLogin = Root + "/auth/google/login";
    public const string GoogleLink = Root + "/auth/google/link";

    public const string AdminUsers = Root + "/admin/users";
    public const string AdminSecurity = Root + "/admin/security";
    public const string AdminLoginLogs = Root + "/admin/logs/logins";
    public const string AdminActivityLogs = Root + "/admin/logs/activity";

    public const string Users = Root + "/users";
    public const string UserGroups = Root + "/user-groups";
    public const string Settings = Root + "/settings";
    public const string Projects = Root + "/projects";
    public const string ProjectGroups = Root + "/project-groups";
    public const string AllTasks = Root + "/tasks";
    public const string AttachmentLimits = Root + "/attachments/limits";

    public static string AdminUser(Guid userId)
    {
        return $"{AdminUsers}/{userId}";
    }

    public static string AdminUserPassword(Guid userId)
    {
        return $"{AdminUsers}/{userId}/password";
    }

    public static string AdminUserRoles(Guid userId)
    {
        return $"{AdminUsers}/{userId}/roles";
    }

    public static string AdminUserLockout(Guid userId)
    {
        return $"{AdminUsers}/{userId}/lockout";
    }

    public static string UserGroup(Guid groupId)
    {
        return $"{UserGroups}/{groupId}";
    }

    public static string UserGroupMembers(Guid groupId)
    {
        return $"{UserGroups}/{groupId}/members";
    }

    public static string UserGroupMember(Guid groupId, Guid userId)
    {
        return $"{UserGroups}/{groupId}/members/{userId}";
    }

    public static string Setting(string key)
    {
        return $"{Settings}/{Uri.EscapeDataString(key)}";
    }

    public static string Project(Guid projectId)
    {
        return $"{Projects}/{projectId}";
    }

    public static string ProjectGroupOf(Guid projectId)
    {
        return $"{Projects}/{projectId}/group";
    }

    public static string ProjectMembers(Guid projectId)
    {
        return $"{Projects}/{projectId}/members";
    }

    public static string ProjectMember(Guid projectId, Guid userId)
    {
        return $"{Projects}/{projectId}/members/{userId}";
    }

    public static string ProjectGroup(Guid projectGroupId)
    {
        return $"{ProjectGroups}/{projectGroupId}";
    }

    public static string ProjectGroupProjects(Guid projectGroupId)
    {
        return $"{ProjectGroups}/{projectGroupId}/projects";
    }

    public static string Tasks(Guid projectId)
    {
        return $"{Projects}/{projectId}/tasks";
    }

    public static string Task(Guid projectId, Guid taskId)
    {
        return $"{Projects}/{projectId}/tasks/{taskId}";
    }

    public static string SubTasks(Guid projectId, Guid taskId)
    {
        return $"{Projects}/{projectId}/tasks/{taskId}/subtasks";
    }

    public static string StartTask(Guid projectId, Guid taskId)
    {
        return $"{Projects}/{projectId}/tasks/{taskId}/start";
    }

    public static string CompleteTask(Guid projectId, Guid taskId)
    {
        return $"{Projects}/{projectId}/tasks/{taskId}/complete";
    }

    public static string Attachments(Guid projectId, Guid taskId)
    {
        return $"{Projects}/{projectId}/tasks/{taskId}/attachments";
    }

    public static string Attachment(Guid projectId, Guid taskId, Guid attachmentId)
    {
        return $"{Projects}/{projectId}/tasks/{taskId}/attachments/{attachmentId}";
    }

    public static string AttachmentContent(Guid projectId, Guid taskId, Guid attachmentId)
    {
        return $"{Projects}/{projectId}/tasks/{taskId}/attachments/{attachmentId}/content";
    }

    public static string AttachmentsBySha256(string sha256, Guid taskId)
    {
        return $"{Root}/attachments/{sha256}?taskId={taskId}";
    }

    public static string AttachmentContentBySha256(string sha256, Guid taskId)
    {
        return $"{Root}/attachments/{sha256}/content?taskId={taskId}";
    }
}
