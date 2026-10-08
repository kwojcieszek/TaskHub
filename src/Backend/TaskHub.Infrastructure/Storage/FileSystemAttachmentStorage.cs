using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Storage;

public class FileSystemAttachmentStorage : IAttachmentStorage
{
    private const int BufferSize = 81920;

    private readonly string _rootPath;

    public FileSystemAttachmentStorage(IOptions<AttachmentStorageOptions> options, IHostEnvironment environment)
    {
        // A relative root is resolved against the application content root.
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.FileSystem.RootPath));
    }

    public Task<bool> ExistsAsync(string sha256, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(GetPath(sha256)));
    }

    public async Task SaveAsync(string sha256, Stream content, CancellationToken cancellationToken = default)
    {
        var path = GetPath(sha256);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written under a temporary name first so a failed upload never leaves a truncated file under the hash.
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var target = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(target, cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task<Stream> OpenReadAsync(string sha256, CancellationToken cancellationToken = default)
    {
        var path = GetPath(sha256);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Attachment content is missing from storage.", path);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string sha256, CancellationToken cancellationToken = default)
    {
        var path = GetPath(sha256);

        // File.Delete ignores a missing file, but not a missing directory above it.
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string sha256)
    {
        return Path.Combine(_rootPath, Path.Combine(AttachmentStoragePath.Segments(sha256)));
    }
}
