using FluentFTP;
using Microsoft.Extensions.Options;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Storage;

public class FtpAttachmentStorage : IAttachmentStorage
{
    private const int BufferSize = 81920;

    private readonly FtpStorageOptions _options;

    public FtpAttachmentStorage(IOptions<AttachmentStorageOptions> options)
    {
        _options = options.Value.Ftp;
    }

    public async Task<bool> ExistsAsync(string sha256, CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectAsync(cancellationToken);

        return await client.FileExists(GetPath(sha256), cancellationToken);
    }

    public async Task SaveAsync(string sha256, Stream content, CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectAsync(cancellationToken);

        var status = await client.UploadStream(content, GetPath(sha256), FtpRemoteExists.Overwrite, true, null, cancellationToken);

        if (status == FtpStatus.Failed)
        {
            throw new IOException("Uploading the attachment to the FTP storage failed.");
        }
    }

    public async Task<Stream> OpenReadAsync(string sha256, CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectAsync(cancellationToken);

        // Downloaded to a temporary file so the FTP connection can be closed before the response is streamed.
        var buffer = new FileStream(
            Path.GetTempFileName(),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            BufferSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        try
        {
            var downloaded = await client.DownloadStream(buffer, GetPath(sha256), 0, null, cancellationToken);

            if (!downloaded)
            {
                throw new FileNotFoundException("Attachment content is missing from storage.", sha256);
            }

            buffer.Position = 0;

            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    public async Task DeleteAsync(string sha256, CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectAsync(cancellationToken);

        var path = GetPath(sha256);

        if (await client.FileExists(path, cancellationToken))
        {
            await client.DeleteFile(path, cancellationToken);
        }
    }

    private async Task<AsyncFtpClient> ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new AsyncFtpClient(_options.Host, _options.UserName, _options.Password, _options.Port);
        client.Config.EncryptionMode = _options.UseSsl ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;

        try
        {
            await client.Connect(cancellationToken);
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }

        return client;
    }

    private string GetPath(string sha256)
    {
        var root = _options.RootPath.Replace('\\', '/').TrimEnd('/');

        return $"{root}/{string.Join('/', AttachmentStoragePath.Segments(sha256))}";
    }
}
