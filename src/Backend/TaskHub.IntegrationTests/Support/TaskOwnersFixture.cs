using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// An API instance with several users who each own a private project with tasks. The cross-user tests only
/// perform attacks that must be refused, so the same users and tasks serve every one of them.
/// </summary>
public sealed class TaskOwnersFixture : IAsyncLifetime
{
    public const int OwnerCount = 4;

    private readonly List<TaskOwner> _owners = new();

    public TaskHubApiFactory Factory { get; } = new();

    public IReadOnlyList<TaskOwner> Owners => _owners;

    public async Task InitializeAsync()
    {
        for (var index = 0; index < OwnerCount; index++)
        {
            _owners.Add(await CreateOwnerAsync($"owner{index}"));
        }
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>Creates one more user with a private project and tasks, for a test that changes what it is given.</summary>
    public async Task<TaskOwner> CreateOwnerAsync(string prefix)
    {
        var session = await Factory.CreateUserAsync(prefix);
        var secret = $"secret-{Guid.NewGuid():N}";

        var project = await session.CreateProjectAsync($"Project {secret}");
        var parentTask = await session.CreateTaskAsync(project.Id, $"Parent {secret}");
        var subTask = await session.CreateSubTaskAsync(project.Id, parentTask.Id, $"Subtask {secret}");

        var ownTask = await session.PostAsync<TaskItemDto>(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest
        {
            Title = $"Own {secret}",
            Description = $"Description {secret}",
            DueDate = DateTime.UtcNow.AddDays(1),
            Priority = TaskItemPriority.High,
            AssignedUserId = session.UserId
        });

        // Unique content, so the hash of this attachment never matches the file of another user.
        var content = Guid.NewGuid().ToByteArray();
        var attachment = await session.UploadAttachmentAsync(project.Id, ownTask.Id, $"{secret}.txt", content, "text/plain");

        var projectComment = await session.CommentOnProjectAsync(project.Id, $"Project comment {secret}");
        var taskComment = await session.CommentOnTaskAsync(project.Id, ownTask.Id, $"Task comment {secret}");

        return new TaskOwner(session, secret, project, parentTask, subTask, ownTask, attachment, content, projectComment, taskComment);
    }
}
