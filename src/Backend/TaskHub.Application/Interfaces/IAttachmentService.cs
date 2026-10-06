using TaskHub.Application.Dtos.Attachments;

namespace TaskHub.Application.Interfaces;

public interface IAttachmentService
{
    Task<AttachmentLimitsDto> GetLimitsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AttachmentDto>> GetByTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    Task<AttachmentDto> UploadAsync(Guid projectId, Guid taskId, Stream content, string fileName, string? contentType, long? declaredSize = null, CancellationToken cancellationToken = default);

    Task<AttachmentContentDto> DownloadAsync(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AttachmentDto>> FindBySha256Async(Guid taskId, string sha256, CancellationToken cancellationToken = default);

    Task<AttachmentContentDto> DownloadBySha256Async(Guid taskId, string sha256, CancellationToken cancellationToken = default);

    Task RemoveUnreferencedContentAsync(IReadOnlyCollection<string> sha256Hashes, CancellationToken cancellationToken = default);
}
