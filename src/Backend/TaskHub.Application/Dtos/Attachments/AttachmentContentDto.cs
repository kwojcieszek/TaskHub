namespace TaskHub.Application.Dtos.Attachments;

public class AttachmentContentDto
{
    public Stream Content { get; set; } = Stream.Null;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
}
