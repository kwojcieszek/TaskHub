using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Listing, downloading, uploading and deleting the attachments of a task, tried by every kind of caller.
/// </summary>
public class AttachmentAccessMatrixTests : AccessMatrixTestsBase
{
    public AttachmentAccessMatrixTests(TaskHubApiFactory factory) : base(factory)
    {
    }

    public static TheoryData<string, Actor> Cases => AccessOperations.Cases(AccessArea.Attachment);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Operation_IsAllowedOnlyWithSufficientAccess(string operation, Actor actor)
    {
        await RunAsync(operation, actor);
    }
}
