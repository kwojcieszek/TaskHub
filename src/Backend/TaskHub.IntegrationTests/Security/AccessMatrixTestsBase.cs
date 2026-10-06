using System.Net;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Runs one operation as one kind of caller against a fresh project and checks both the answer
/// and, when the caller was refused, that nothing in the project changed.
/// </summary>
public abstract class AccessMatrixTestsBase : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    protected AccessMatrixTestsBase(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    protected async Task RunAsync(string operationName, Actor actor)
    {
        var operation = AccessOperations.Get(operationName);
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var caller = scenario.Cast.For(actor);

        var response = await operation.Execute(caller, scenario);

        await response.ShouldBeAsync(operation.ExpectedFor(actor));

        if (!operation.IsAllowedFor(actor))
        {
            await AssertUntouchedAsync(scenario);
        }
    }

    private static async Task AssertUntouchedAsync(ProjectScenario scenario)
    {
        var owner = scenario.Cast.Owner;

        var project = await owner.GetAsync<ProjectDto>(ApiRoutes.Project(scenario.Project.Id));
        Assert.Equal(scenario.Project.Name, project.Name);
        Assert.True(project.IsActive);
        Assert.Equal(2, project.TaskCount);
        Assert.Equal(1, project.SubTaskCount);

        var members = await owner.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(scenario.Project.Id));
        Assert.Equal(3, members.Count);

        var leafTask = await owner.GetAsync<TaskItemDto>(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id));
        Assert.Equal(scenario.LeafTask.Title, leafTask.Title);
        Assert.Equal(scenario.LeafTask.Status, leafTask.Status);

        var attachments = await owner.GetAsync<List<AttachmentDto>>(ApiRoutes.Attachments(scenario.Project.Id, scenario.LeafTask.Id));
        Assert.Equal(scenario.Attachment.Id, Assert.Single(attachments).Id);

        var content = await owner.GetAsync(ApiRoutes.AttachmentContent(scenario.Project.Id, scenario.LeafTask.Id, scenario.Attachment.Id));
        await content.ShouldBeAsync(HttpStatusCode.OK);
    }
}
