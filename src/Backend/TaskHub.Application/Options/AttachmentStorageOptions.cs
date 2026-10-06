namespace TaskHub.Application.Options;

public class AttachmentStorageOptions
{
    public const string SectionName = "AttachmentStorage";

    public AttachmentStorageProvider Provider { get; set; } = AttachmentStorageProvider.FileSystem;

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    public FileSystemStorageOptions FileSystem { get; set; } = new();

    public FtpStorageOptions Ftp { get; set; } = new();
}
