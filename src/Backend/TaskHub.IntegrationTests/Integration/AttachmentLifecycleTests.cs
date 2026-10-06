using System.Net;
using System.Security.Cryptography;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Integration;

/// <summary>
/// Attachments with the real file storage: uploaded, found, downloaded and removed again.
/// </summary>
public class AttachmentLifecycleTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public AttachmentLifecycleTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadedFile_IsDescribedByItsContent()
    {
        var (user, project, task) = await CreateTaskAsync();
        var content = RandomContent(300);

        var attachment = await user.UploadAttachmentAsync(project.Id, task.Id, "report.pdf", content, "application/pdf");

        Assert.Equal("report.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(300, attachment.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), attachment.Sha256);
        Assert.Equal(user.UserId, attachment.UploadedById);
        Assert.Equal(task.Id, attachment.TaskItemId);
        Assert.Equal(project.Id, attachment.ProjectId);

        var reloadedTask = await user.GetAsync<TaskItemDto>(ApiRoutes.Task(project.Id, task.Id));
        Assert.Equal(1, reloadedTask.AttachmentCount);
    }

    [Fact]
    public async Task UploadedFile_IsDownloadedUnchanged()
    {
        var (user, project, task) = await CreateTaskAsync();
        var content = RandomContent(1000);
        var attachment = await user.UploadAttachmentAsync(project.Id, task.Id, "data.bin", content);

        var byId = await user.GetAsync(ApiRoutes.AttachmentContent(project.Id, task.Id, attachment.Id));
        var byHash = await user.GetAsync(ApiRoutes.AttachmentContentBySha256(attachment.Sha256, task.Id));
        var byUpperCaseHash = await user.GetAsync(ApiRoutes.AttachmentContentBySha256(attachment.Sha256.ToUpperInvariant(), task.Id));

        foreach (var response in new[] { byId, byHash, byUpperCaseHash })
        {
            await response.ShouldBeAsync(HttpStatusCode.OK);
            Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
            Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("data.bin", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        }
    }

    [Fact]
    public async Task Attachments_AreListedPerTaskAndFoundByHash()
    {
        var (user, project, task) = await CreateTaskAsync();
        var otherTask = await user.CreateTaskAsync(project.Id, "Other");
        var first = await user.UploadAttachmentAsync(project.Id, task.Id, "first.txt", RandomContent(10));
        var second = await user.UploadAttachmentAsync(project.Id, task.Id, "second.txt", RandomContent(20));
        await user.UploadAttachmentAsync(project.Id, otherTask.Id, "elsewhere.txt", RandomContent(30));

        var list = await user.GetAsync<List<AttachmentDto>>(ApiRoutes.Attachments(project.Id, task.Id));
        var found = await user.GetAsync<List<AttachmentDto>>(ApiRoutes.AttachmentsBySha256(second.Sha256, task.Id));
        var unknownHash = await user.GetAsync<List<AttachmentDto>>(ApiRoutes.AttachmentsBySha256(new string('0', 64), task.Id));

        Assert.Equal(new[] { first.Id, second.Id }, list.Select(attachment => attachment.Id));
        Assert.Equal(second.Id, Assert.Single(found).Id);
        Assert.Empty(unknownHash);
    }

    [Fact]
    public async Task SameContent_IsStoredOnceAndSurvivesUntilItsLastAttachmentIsDeleted()
    {
        var (user, project, task) = await CreateTaskAsync();
        var otherTask = await user.CreateTaskAsync(project.Id, "Other");
        var content = RandomContent(64);

        var first = await user.UploadAttachmentAsync(project.Id, task.Id, "original.txt", content);
        var second = await user.UploadAttachmentAsync(project.Id, otherTask.Id, "copy.txt", content);

        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Single(StoredFiles(first.Sha256));

        var deleteFirst = await user.DeleteAsync(ApiRoutes.Attachment(project.Id, task.Id, first.Id));
        await deleteFirst.ShouldBeAsync(HttpStatusCode.NoContent);

        var copy = await user.GetAsync(ApiRoutes.AttachmentContent(project.Id, otherTask.Id, second.Id));
        await copy.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(content, await copy.Content.ReadAsByteArrayAsync());

        var deleteSecond = await user.DeleteAsync(ApiRoutes.Attachment(project.Id, otherTask.Id, second.Id));
        await deleteSecond.ShouldBeAsync(HttpStatusCode.NoContent);

        Assert.Empty(StoredFiles(first.Sha256));
    }

    [Fact]
    public async Task DeletedAttachment_IsGone()
    {
        var (user, project, task) = await CreateTaskAsync();
        var attachment = await user.UploadAttachmentAsync(project.Id, task.Id, "gone.txt", RandomContent(16));

        var delete = await user.DeleteAsync(ApiRoutes.Attachment(project.Id, task.Id, attachment.Id));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var download = await user.GetAsync(ApiRoutes.AttachmentContent(project.Id, task.Id, attachment.Id));
        var byHash = await user.GetAsync(ApiRoutes.AttachmentContentBySha256(attachment.Sha256, task.Id));
        var deleteAgain = await user.DeleteAsync(ApiRoutes.Attachment(project.Id, task.Id, attachment.Id));
        var list = await user.GetAsync<List<AttachmentDto>>(ApiRoutes.Attachments(project.Id, task.Id));

        await download.ShouldBeAsync(HttpStatusCode.NotFound);
        await byHash.ShouldBeAsync(HttpStatusCode.NotFound);
        await deleteAgain.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Empty(list);
    }

    [Fact]
    public async Task DeletedTask_TakesItsAttachmentsAndTheirFilesAlong()
    {
        var (user, project, task) = await CreateTaskAsync();
        var subTask = await user.CreateSubTaskAsync(project.Id, task.Id);
        var onTask = await user.UploadAttachmentAsync(project.Id, task.Id, "task.txt", RandomContent(16));
        var onSubTask = await user.UploadAttachmentAsync(project.Id, subTask.Id, "subtask.txt", RandomContent(16));

        var delete = await user.DeleteAsync(ApiRoutes.Task(project.Id, task.Id));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var list = await user.GetAsync(ApiRoutes.Attachments(project.Id, task.Id));
        await list.ShouldBeAsync(HttpStatusCode.NotFound);
        Assert.Empty(StoredFiles(onTask.Sha256));
        Assert.Empty(StoredFiles(onSubTask.Sha256));
    }

    [Fact]
    public async Task Limits_ReportTheUsageOfTheCaller()
    {
        var (user, project, task) = await CreateTaskAsync();

        var before = await user.GetAsync<AttachmentLimitsDto>(ApiRoutes.AttachmentLimits);
        var attachment = await user.UploadAttachmentAsync(project.Id, task.Id, "first.bin", RandomContent(200));
        await user.UploadAttachmentAsync(project.Id, task.Id, "second.bin", RandomContent(50));
        var afterUploads = await user.GetAsync<AttachmentLimitsDto>(ApiRoutes.AttachmentLimits);
        await user.DeleteAsync(ApiRoutes.Attachment(project.Id, task.Id, attachment.Id));
        var afterDelete = await user.GetAsync<AttachmentLimitsDto>(ApiRoutes.AttachmentLimits);

        Assert.Equal(TaskHubApiFactory.MaxFileSizeBytes, before.MaxFileSizeBytes);
        Assert.Equal(0, before.UsedBytes);
        Assert.Equal(250, afterUploads.UsedBytes);
        Assert.Equal(50, afterDelete.UsedBytes);
    }

    [Fact]
    public async Task Quota_StopsUploadsOnceTheUsersTotalIsReached()
    {
        using var factory = new TaskHubApiFactory().WithSetting("AttachmentStorage:MaxTotalBytesPerUser", "1000");
        var user = await factory.CreateUserAsync();
        var other = await factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();
        var task = await user.CreateTaskAsync(project.Id);
        await user.AddMemberAsync(project.Id, other.UserId, ProjectMemberRole.Editor);
        var url = ApiRoutes.Attachments(project.Id, task.Id);

        var first = await user.UploadAttachmentAsync(project.Id, task.Id, "first.bin", RandomContent(600));
        var overQuota = await user.UploadAsync(url, "second.bin", RandomContent(600));
        var errors = await overQuota.ReadErrorsAsync(HttpStatusCode.BadRequest);
        Assert.Contains("quota", errors[0]);

        // The quota is per user: a colleague can still upload to the same task.
        var byOther = await other.UploadAsync(url, "other.bin", RandomContent(600));
        await byOther.ShouldBeAsync(HttpStatusCode.OK);

        // Deleting frees the space again.
        await user.DeleteAsync(ApiRoutes.Attachment(project.Id, task.Id, first.Id));
        var afterDelete = await user.UploadAsync(url, "second.bin", RandomContent(600));
        await afterDelete.ShouldBeAsync(HttpStatusCode.OK);
    }

    private static byte[] RandomContent(int size)
    {
        return RandomNumberGenerator.GetBytes(size);
    }

    private string[] StoredFiles(string sha256)
    {
        if (!Directory.Exists(_factory.AttachmentsPath))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(_factory.AttachmentsPath, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith(sha256, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private async Task<(ApiSession User, ProjectDto Project, TaskItemDto Task)> CreateTaskAsync()
    {
        var user = await _factory.CreateUserAsync();
        var project = await user.CreateProjectAsync();
        var task = await user.CreateTaskAsync(project.Id);

        return (user, project, task);
    }
}
