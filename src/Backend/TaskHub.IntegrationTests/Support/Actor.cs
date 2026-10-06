namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Who calls the API in a permission test, in relation to the project under test.
/// </summary>
public enum Actor
{
    Anonymous,
    Stranger,
    Viewer,
    Editor,
    Owner,
    Admin
}
