namespace TaskHub.Application.Interfaces;

/// <summary>
/// Content-addressed file storage: every file is stored once under its lower-case SHA-256 hash.
/// </summary>
public interface IAttachmentStorage
{
    Task<bool> ExistsAsync(string sha256, CancellationToken cancellationToken = default);

    Task SaveAsync(string sha256, Stream content, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string sha256, CancellationToken cancellationToken = default);

    Task DeleteAsync(string sha256, CancellationToken cancellationToken = default);
}
