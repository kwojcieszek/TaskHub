using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Reading and writing the comments of a project and of its tasks, tried by every kind of caller.
/// </summary>
public class CommentAccessMatrixTests : AccessMatrixTestsBase
{
    public CommentAccessMatrixTests(TaskHubApiFactory factory) : base(factory)
    {
    }

    public static TheoryData<string, Actor> Cases => AccessOperations.Cases(AccessArea.Comment);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Operation_IsAllowedOnlyWithSufficientAccess(string operation, Actor actor)
    {
        await RunAsync(operation, actor);
    }
}
