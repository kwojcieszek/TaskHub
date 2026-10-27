using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class AttachmentQuotaTests : IDisposable
{
    private const long Quota = 100;

    private readonly TestHost _host = new(configureAttachments: options => options.MaxTotalBytesPerUser = Quota);
    private readonly IAttachmentService _attachments;

    public AttachmentQuotaTests()
    {
        _attachments = _host.Get<IAttachmentService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Upload_IsAcceptedUpToExactlyTheQuota()
    {
        var (project, task, _) = await ArrangeTaskAsync();

        await UploadAsync(project.Id, task.Id, "a.bin", 60);
        await UploadAsync(project.Id, task.Id, "b.bin", 40);

        var limits = await _attachments.GetLimitsAsync();

        Assert.Equal(Quota, limits.MaxTotalBytesPerUser);
        Assert.Equal(Quota, limits.UsedBytes);
    }

    [Fact]
    public async Task Upload_IsRefusedWhenItWouldExceedTheQuota()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        await UploadAsync(project.Id, task.Id, "a.bin", 60);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => UploadAsync(project.Id, task.Id, "b.bin", 41));

        Assert.Contains("quota", Assert.Single(exception.Errors));
        Assert.Equal(1, _host.Storage.Count);
    }

    [Fact]
    public async Task Upload_StillCountsTheContentWhenTheDeclaredSizeUnderstatesIt()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        using var stream = new MemoryStream(new byte[Quota + 1]);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _attachments.UploadAsync(
            project.Id, task.Id, stream, "large.bin", "application/octet-stream", declaredSize: 10));

        Assert.Equal(0, _host.Storage.Count);
    }

    [Fact]
    public async Task Delete_FreesTheSpaceForTheNextUpload()
    {
        var (project, task, _) = await ArrangeTaskAsync();
        var first = await UploadAsync(project.Id, task.Id, "a.bin", 80);

        await _attachments.DeleteAsync(project.Id, task.Id, first.Id);
        await UploadAsync(project.Id, task.Id, "b.bin", 80);

        Assert.Equal(80, (await _attachments.GetLimitsAsync()).UsedBytes);
    }

    [Fact]
    public async Task Quota_IsCountedSeparatelyForEachUser()
    {
        var (project, task, owner) = await ArrangeTaskAsync();
        var editor = await _host.CreateUserAsync("editor");
        await _host.AddMemberAsync(project, owner, editor, ProjectMemberRole.Editor);

        await UploadAsync(project.Id, task.Id, "owner.bin", 90);

        _host.SignIn(editor);
        await UploadAsync(project.Id, task.Id, "editor.bin", 90);

        Assert.Equal(90, (await _attachments.GetLimitsAsync()).UsedBytes);
    }

    [Fact]
    public async Task Quota_OfZeroMeansNoLimit()
    {
        using var host = new TestHost(configureAttachments: options => options.MaxTotalBytesPerUser = 0);
        var owner = await host.CreateUserAsync("owner");
        var project = await host.CreateProjectAsync(owner);
        var task = await host.Get<ITaskItemService>().CreateAsync(project.Id, new CreateTaskItemRequest { Title = "task" });
        var attachments = host.Get<IAttachmentService>();

        for (var index = 0; index < 3; index++)
        {
            using var stream = new MemoryStream(CreateContent(200, index));
            await attachments.UploadAsync(project.Id, task.Id, stream, $"file{index}.bin", "application/octet-stream", stream.Length);
        }

        Assert.Equal(600, (await attachments.GetLimitsAsync()).UsedBytes);
    }

    private async Task<(ProjectDto Project, TaskItemDto Task, User Owner)> ArrangeTaskAsync()
    {
        var owner = await _host.CreateUserAsync("owner");
        var project = await _host.CreateProjectAsync(owner);
        var task = await _host.Get<ITaskItemService>().CreateAsync(project.Id, new CreateTaskItemRequest { Title = "task" });

        return (project, task, owner);
    }

    private async Task<AttachmentDto> UploadAsync(Guid projectId, Guid taskId, string fileName, int size)
    {
        using var stream = new MemoryStream(CreateContent(size, fileName.Length + size));

        return await _attachments.UploadAsync(projectId, taskId, stream, fileName, "application/octet-stream", stream.Length);
    }

    // Different seeds give different content, so each upload is stored under its own hash.
    private static byte[] CreateContent(int size, int seed)
    {
        var content = new byte[size];
        new Random(seed).NextBytes(content);

        return content;
    }
}
