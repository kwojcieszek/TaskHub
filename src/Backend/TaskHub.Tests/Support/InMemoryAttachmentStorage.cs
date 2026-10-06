using TaskHub.Application.Interfaces;

namespace TaskHub.Tests.Support;

/// <summary>
/// Keeps attachment content in memory, keyed by hash like the real storages.
/// </summary>
public class InMemoryAttachmentStorage : IAttachmentStorage
{
    private readonly Dictionary<string, byte[]> _files = new();

    public int Count => _files.Count;

    // How many times content was actually written, to tell deduplicated uploads apart.
    public int SaveCalls { get; private set; }

    public Task<bool> ExistsAsync(string sha256, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_files.ContainsKey(sha256));
    }

    public async Task SaveAsync(string sha256, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        _files[sha256] = buffer.ToArray();
        SaveCalls++;
    }

    public Task<Stream> OpenReadAsync(string sha256, CancellationToken cancellationToken = default)
    {
        if (!_files.TryGetValue(sha256, out var content))
        {
            throw new FileNotFoundException("Attachment content is missing from storage.", sha256);
        }

        return Task.FromResult<Stream>(new MemoryStream(content));
    }

    public Task DeleteAsync(string sha256, CancellationToken cancellationToken = default)
    {
        _files.Remove(sha256);

        return Task.CompletedTask;
    }
}
