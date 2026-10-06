namespace TaskHub.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; }

    public Guid TaskItemId { get; set; }

    public TaskItem? TaskItem { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public Guid UploadedById { get; set; }

    public ApplicationUser? UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
