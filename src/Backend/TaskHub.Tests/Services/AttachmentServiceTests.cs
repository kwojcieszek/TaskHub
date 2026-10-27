using System.Security.Cryptography;
using System.Text;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class AttachmentServiceTests : IDisposable
{
    private const string Content = "attachment content";

    private readonly TestHost _host = new();
    private readonly IAttachmentService _attachments;

    public AttachmentServiceTests()
    {
        _attachments = _host.Get<IAttachmentService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task GetLimits_ReportsTheConfiguredMaximumFileSize()
    {
        var limits = await _attachments.GetLimitsAsync();

        Assert.Equal(TestHost.MaxFileSizeBytes, limits.MaxFileSizeBytes);
    }

    [Fact]
    public async Task Upload_AcceptsAFileOfExactlyTheMaximumSize()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        var atLimit = new string('x', (int)TestHost.MaxFileSizeBytes);

        var attachment = await UploadAsync(project.Id, task.Id, "limit.txt", atLimit);

        Assert.Equal(TestHost.MaxFileSizeBytes, attachment.Size);
    }

    [Fact]
    public async Task Upload_RefusesAnOversizedFileByItsDeclaredSizeWithoutReadingIt()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Content));

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => _attachments.UploadAsync(
            project.Id, task.Id, stream, "large.txt", "text/plain", declaredSize: TestHost.MaxFileSizeBytes + 1));

        Assert.Equal(0, stream.Position);
        Assert.Contains("too large", Assert.Single(exception.Errors));
        Assert.Equal(0, _host.Storage.Count);
    }

    [Fact]
    public async Task Upload_StillCountsTheContentWhenTheDeclaredSizeUnderstatesIt()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        using var stream = new MemoryStream(new byte[TestHost.MaxFileSizeBytes + 1]);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _attachments.UploadAsync(
            project.Id, task.Id, stream, "large.bin", "application/octet-stream", declaredSize: 10));

        Assert.Equal(0, _host.Storage.Count);
    }

    [Fact]
    public async Task Upload_StoresContentUnderItsSha256()
    {
        var (project, task, _) = await ArrangeTaskAsync();

        var attachment = await UploadAsync(project.Id, task.Id, "notes.txt");

        Assert.Equal(Sha256Of(Content), attachment.Sha256);
        Assert.Equal(Encoding.UTF8.GetByteCount(Content), attachment.Size);
        Assert.Equal(1, _host.Storage.Count);
    }

    [Fact]
    public async Task Upload_KeepsOnlyTheFileNameFromASuppliedPath()
    {
        var (project, task, _) = await ArrangeTaskAsync();

        var attachment = await UploadAsync(project.Id, task.Id, "../../secret/notes.txt");

        Assert.Equal("notes.txt", attachment.FileName);
    }

    [Fact]
    public async Task Upload_RejectsEmptyAndOversizedFiles()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        var tooLarge = new string('x', (int)TestHost.MaxFileSizeBytes + 1);

        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadAsync(project.Id, task.Id, "empty.txt", string.Empty));
        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadAsync(project.Id, task.Id, "large.txt", tooLarge));
        Assert.Equal(0, _host.Storage.Count);
    }

    [Fact]
    public async Task Upload_IsForbiddenForAViewer()
    {
        var (project, task, owner) = await ArrangeTaskAsync();
        var viewer = await _host.CreateUserAsync("viewer");
        await _host.AddMemberAsync(project, owner, viewer, ProjectMemberRole.Viewer);

        _host.SignIn(viewer);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => UploadAsync(project.Id, task.Id, "notes.txt"));
    }

    [Fact]
    public async Task IdenticalContent_IsStoredOnceAndRemovedWithItsLastAttachment()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        var otherTask = await _host.Get<ITaskItemService>().CreateAsync(project.Id, new CreateTaskItemRequest { Title = "other" });

        var first = await UploadAsync(project.Id, task.Id, "a.txt");
        var second = await UploadAsync(project.Id, otherTask.Id, "b.txt");
        var savedOnce = _host.Storage.SaveCalls;

        await _attachments.DeleteAsync(project.Id, task.Id, first.Id);
        var afterFirstDelete = _host.Storage.Count;
        await _attachments.DeleteAsync(project.Id, otherTask.Id, second.Id);

        Assert.Equal(1, savedOnce);
        Assert.Equal(1, afterFirstDelete);
        Assert.Equal(0, _host.Storage.Count);
    }

    [Fact]
    public async Task DownloadBySha256_ReturnsContentForTheTaskItIsAttachedTo()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        var attachment = await UploadAsync(project.Id, task.Id, "notes.txt");

        var download = await _attachments.DownloadBySha256Async(task.Id, attachment.Sha256.ToUpperInvariant());
        using var reader = new StreamReader(download.Content);

        Assert.Equal(Content, await reader.ReadToEndAsync());
        Assert.Equal("notes.txt", download.FileName);
    }

    [Fact]
    public async Task DownloadBySha256_DoesNotServeContentThroughAnotherTask()
    {
        // The hash of a file in a foreign project is useless without access to a task it is attached to.
        var (project, task, _) = await ArrangeTaskAsync();
        var attachment = await UploadAsync(project.Id, task.Id, "notes.txt");
        var stranger = await _host.CreateUserAsync("stranger");
        var strangersProject = await _host.CreateProjectAsync(stranger, "theirs");
        var strangersTask = await _host.Get<ITaskItemService>().CreateAsync(strangersProject.Id, new CreateTaskItemRequest { Title = "theirs" });

        await Assert.ThrowsAsync<NotFoundException>(() => _attachments.DownloadBySha256Async(strangersTask.Id, attachment.Sha256));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _attachments.DownloadBySha256Async(task.Id, attachment.Sha256));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("../../../../../../../../../../../../../../../../../../../../etc")]
    public async Task FindBySha256_RejectsAnythingThatIsNotASha256(string sha256)
    {
        var (_, task, _) = await ArrangeTaskAsync();

        await Assert.ThrowsAsync<ValidationFailedException>(() => _attachments.FindBySha256Async(task.Id, sha256));
    }

    [Fact]
    public async Task DeletingATask_RemovesItsAttachmentContent()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        await UploadAsync(project.Id, task.Id, "notes.txt");

        await _host.Get<ITaskItemService>().DeleteAsync(project.Id, task.Id);

        Assert.Equal(0, _host.Storage.Count);
    }

    private async Task<(ProjectDto Project, TaskItemDto Task, User Owner)> ArrangeTaskAsync()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner);
        var task = await _host.Get<ITaskItemService>().CreateAsync(project.Id, new CreateTaskItemRequest { Title = "task" });

        return (project, task, owner);
    }

    private async Task<Application.Dtos.Attachments.AttachmentDto> UploadAsync(Guid projectId, Guid taskId, string fileName, string content = Content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        return await _attachments.UploadAsync(projectId, taskId, stream, fileName, "text/plain", stream.Length);
    }

    private static string Sha256Of(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
}
