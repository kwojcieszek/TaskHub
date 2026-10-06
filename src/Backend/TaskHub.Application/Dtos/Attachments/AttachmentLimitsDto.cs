namespace TaskHub.Application.Dtos.Attachments;

public class AttachmentLimitsDto
{
    public long MaxFileSizeBytes { get; set; }

    // 0 means the caller's attachments have no total limit.
    public long MaxTotalBytesPerUser { get; set; }

    public long UsedBytes { get; set; }
}
