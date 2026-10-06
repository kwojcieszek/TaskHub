using System.Security.Cryptography;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class AttachmentService : IAttachmentService
{
    private const int MaxFileNameLength = 255;
    private const int Sha256HexLength = 64;
    private const int BufferSize = 81920;
    private const string DefaultContentType = "application/octet-stream";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IAttachmentStorage _storage;
    private readonly IMapper _mapper;
    private readonly ILogger<AttachmentService> _logger;
    private readonly AttachmentStorageOptions _options;

    public AttachmentService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IProjectAccessGuard accessGuard,
        IAttachmentStorage storage,
        IMapper mapper,
        ILogger<AttachmentService> logger,
        IOptions<AttachmentStorageOptions> options)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
        _storage = storage;
        _mapper = mapper;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<AttachmentDto>> GetByTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);
        await EnsureTaskExistsAsync(projectId, taskId, cancellationToken);

        var attachments = await WithDetails()
            .Where(a => a.TaskItemId == taskId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<AttachmentDto>>(attachments);
    }

    public async Task<AttachmentDto> UploadAsync(Guid projectId, Guid taskId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);
        await EnsureTaskExistsAsync(projectId, taskId, cancellationToken);

        var safeFileName = Path.GetFileName(fileName ?? string.Empty).Trim();

        if (safeFileName.Length == 0 || safeFileName.Length > MaxFileNameLength)
        {
            throw new ValidationFailedException(new[] { $"File name is required and cannot exceed {MaxFileNameLength} characters." });
        }

        // The hash is the storage key, so the upload is buffered to a temporary file while it is being hashed.
        await using var buffer = new FileStream(
            Path.GetTempFileName(),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            BufferSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        var sha256 = await BufferAndHashAsync(content, buffer, cancellationToken);

        if (!await _storage.ExistsAsync(sha256, cancellationToken))
        {
            buffer.Position = 0;
            await _storage.SaveAsync(sha256, buffer, cancellationToken);
        }

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskId,
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType,
            Size = buffer.Length,
            Sha256 = sha256,
            UploadedById = _currentUser.UserId
        };

        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await WithDetails().FirstAsync(a => a.Id == attachment.Id, cancellationToken);

        return _mapper.Map<AttachmentDto>(created);
    }

    public async Task<AttachmentContentDto> DownloadAsync(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var attachment = await LoadAsync(projectId, taskId, attachmentId, cancellationToken);

        return await OpenContentAsync(attachment, cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);

        var attachment = await _dbContext.Attachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskItemId == taskId && a.TaskItem!.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Attachment not found.");

        _dbContext.Attachments.Remove(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RemoveUnreferencedContentAsync(new[] { attachment.Sha256 }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AttachmentDto>> FindBySha256Async(Guid taskId, string sha256, CancellationToken cancellationToken = default)
    {
        var query = await AccessibleBySha256Async(taskId, sha256, cancellationToken);

        var attachments = await query
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<AttachmentDto>>(attachments);
    }

    public async Task<AttachmentContentDto> DownloadBySha256Async(Guid taskId, string sha256, CancellationToken cancellationToken = default)
    {
        var query = await AccessibleBySha256Async(taskId, sha256, cancellationToken);

        var attachment = await query
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Attachment not found.");

        return await OpenContentAsync(attachment, cancellationToken);
    }

    public async Task RemoveUnreferencedContentAsync(IReadOnlyCollection<string> sha256Hashes, CancellationToken cancellationToken = default)
    {
        foreach (var sha256 in sha256Hashes.Distinct())
        {
            var stillReferenced = await _dbContext.Attachments.AsNoTracking().AnyAsync(a => a.Sha256 == sha256, cancellationToken);

            if (stillReferenced)
            {
                continue;
            }

            try
            {
                await _storage.DeleteAsync(sha256, cancellationToken);
            }
            catch (Exception exception)
            {
                // The database rows are already gone; a leftover file must not fail the request.
                _logger.LogWarning(exception, "Could not remove attachment content {Sha256} from storage", sha256);
            }
        }
    }

    private async Task<string> BufferAndHashAsync(Stream content, Stream buffer, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var chunk = new byte[BufferSize];
        long total = 0;
        int read;

        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;

            if (total > _options.MaxFileSizeBytes)
            {
                throw new ValidationFailedException(new[] { $"File cannot exceed {_options.MaxFileSizeBytes} bytes." });
            }

            hash.AppendData(chunk, 0, read);
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            throw new ValidationFailedException(new[] { "File is empty." });
        }

        await buffer.FlushAsync(cancellationToken);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private async Task<AttachmentContentDto> OpenContentAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        return new AttachmentContentDto
        {
            Content = await _storage.OpenReadAsync(attachment.Sha256, cancellationToken),
            FileName = attachment.FileName,
            ContentType = attachment.ContentType
        };
    }

    private async Task<IQueryable<Attachment>> AccessibleBySha256Async(Guid taskId, string sha256, CancellationToken cancellationToken)
    {
        var normalized = (sha256 ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length != Sha256HexLength || !normalized.All(Uri.IsHexDigit))
        {
            throw new ValidationFailedException(new[] { "SHA-256 must be a 64-character hexadecimal string." });
        }

        var projectId = await _dbContext.Tasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => (Guid?)t.ProjectId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Task not found.");

        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        // The hash alone is not enough: the content must be attached to the task the caller has access to.
        return WithDetails().Where(a => a.Sha256 == normalized && a.TaskItemId == taskId);
    }

    private IQueryable<Attachment> WithDetails()
    {
        return _dbContext.Attachments
            .AsNoTracking()
            .Include(a => a.TaskItem)
            .Include(a => a.UploadedBy);
    }

    private async Task<Attachment> LoadAsync(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken cancellationToken)
    {
        return await WithDetails()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskItemId == taskId && a.TaskItem!.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Attachment not found.");
    }

    private async Task EnsureTaskExistsAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Tasks.AsNoTracking().AnyAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Task not found.");
        }
    }
}
