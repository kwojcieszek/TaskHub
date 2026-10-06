namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// The least a project member needs for an operation: viewers may view, editors may also edit, only the owner manages.
/// </summary>
public enum AccessLevel
{
    View,
    Edit,
    Manage
}
