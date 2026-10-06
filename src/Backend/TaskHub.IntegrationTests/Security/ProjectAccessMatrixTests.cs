using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Reading, changing, sharing and deleting a project, tried by every kind of caller.
/// </summary>
public class ProjectAccessMatrixTests : AccessMatrixTestsBase
{
    public ProjectAccessMatrixTests(TaskHubApiFactory factory) : base(factory)
    {
    }

    public static TheoryData<string, Actor> Cases => AccessOperations.Cases(AccessArea.Project);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Operation_IsAllowedOnlyWithSufficientAccess(string operation, Actor actor)
    {
        await RunAsync(operation, actor);
    }
}
