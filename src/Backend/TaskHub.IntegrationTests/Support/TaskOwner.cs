using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// One ordinary user with a project nobody else belongs to and the tasks in it. In the cross-user tests
/// every such user is in turn the attacker and the victim of every other one.
/// </summary>
public sealed class TaskOwner
{
    public TaskOwner(
        ApiSession session,
        string secret,
        ProjectDto project,
        TaskItemDto parentTask,
        TaskItemDto subTask,
        TaskItemDto ownTask,
        AttachmentDto attachment,
        byte[] attachmentContent,
        CommentDto projectComment,
        CommentDto taskComment)
    {
        Session = session;
        Secret = secret;
        Project = project;
        ParentTask = parentTask;
        SubTask = subTask;
        OwnTask = ownTask;
        Attachment = attachment;
        AttachmentContent = attachmentContent;
        ProjectComment = projectComment;
        TaskComment = taskComment;
    }

    public ApiSession Session { get; }

    // Part of every title and description of this user, so a leak into someone else's response can be recognised.
    public string Secret { get; }

    public ProjectDto Project { get; }

    public TaskItemDto ParentTask { get; }

    public TaskItemDto SubTask { get; }

    // Assigned to the user, due soon and carrying the attachment.
    public TaskItemDto OwnTask { get; }

    public AttachmentDto Attachment { get; }

    public byte[] AttachmentContent { get; }

    public CommentDto ProjectComment { get; }

    // Written on the own task.
    public CommentDto TaskComment { get; }

    public IReadOnlyList<Guid> TaskIds => new[] { ParentTask.Id, SubTask.Id, OwnTask.Id };
}
