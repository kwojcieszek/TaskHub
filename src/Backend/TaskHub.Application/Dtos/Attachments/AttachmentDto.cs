namespace TaskHub.Application.Dtos.Attachments;

public class AttachmentDto
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid TaskItemId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public Guid UploadedById { get; set; }

    public string UploadedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
