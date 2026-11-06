using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Reading, creating, changing and deleting the tasks of a project, tried by every kind of caller.
/// </summary>
public class TaskAccessMatrixTests : AccessMatrixTestsBase
{
    public static TheoryData<string, Actor> Cases => AccessOperations.Cases(AccessArea.Task);

    public TaskAccessMatrixTests(TaskHubApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Operation_IsAllowedOnlyWithSufficientAccess(string operation, Actor actor)
    {
        await RunAsync(operation, actor);
    }
}
