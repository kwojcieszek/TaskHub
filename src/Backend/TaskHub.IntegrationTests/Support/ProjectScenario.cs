using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// A fresh project with one member of every role, a task with a subtask and a task with an attachment.
/// Each permission test gets its own, so destructive operations never affect another test.
/// </summary>
public sealed class ProjectScenario
{
    private ProjectScenario(TestCast cast, ProjectDto project, TaskItemDto parentTask, TaskItemDto subTask, TaskItemDto leafTask, AttachmentDto attachment, byte[] attachmentContent)
    {
        Cast = cast;
        Project = project;
        ParentTask = parentTask;
        SubTask = subTask;
        LeafTask = leafTask;
        Attachment = attachment;
        AttachmentContent = attachmentContent;
    }

    public TestCast Cast { get; }

    public ProjectDto Project { get; }

    public TaskItemDto ParentTask { get; }

    public TaskItemDto SubTask { get; }

    // A task without subtasks, so it can be started, completed and deleted on its own.
    public TaskItemDto LeafTask { get; }

    // Attached to the leaf task.
    public AttachmentDto Attachment { get; }

    public byte[] AttachmentContent { get; }

    /// <summary>Wraps a project a test built by hand, so it can be used like a created scenario.</summary>
    public static ProjectScenario FromExisting(TestCast cast, ProjectDto project, TaskItemDto parentTask, TaskItemDto subTask, TaskItemDto leafTask, AttachmentDto attachment, byte[] attachmentContent)
    {
        return new ProjectScenario(cast, project, parentTask, subTask, leafTask, attachment, attachmentContent);
    }

    public static async Task<ProjectScenario> CreateAsync(TaskHubApiFactory factory)
    {
        var cast = await factory.GetCastAsync();
        var owner = cast.Owner;

        var project = await owner.CreateProjectAsync($"Scenario {Guid.NewGuid():N}");
        await owner.AddMemberAsync(project.Id, cast.Editor.UserId, ProjectMemberRole.Editor);
        await owner.AddMemberAsync(project.Id, cast.Viewer.UserId, ProjectMemberRole.Viewer);

        var parentTask = await owner.CreateTaskAsync(project.Id, "Parent");
        var subTask = await owner.CreateSubTaskAsync(project.Id, parentTask.Id);
        var leafTask = await owner.CreateTaskAsync(project.Id, "Leaf");

        // Unique content, so the hash of this attachment never matches one from another scenario.
        var content = Guid.NewGuid().ToByteArray();
        var attachment = await owner.UploadAttachmentAsync(project.Id, leafTask.Id, "note.txt", content, "text/plain");

        return new ProjectScenario(cast, project, parentTask, subTask, leafTask, attachment, content);
    }
}
