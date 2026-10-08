namespace TaskHub.Application.Dtos.Comments;

public class CommentDto
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    // Empty for a comment on the project itself.
    public Guid? TaskItemId { get; set; }

    // Empty once the author's account has been deleted.
    public Guid? AuthorId { get; set; }

    public string? AuthorDisplayName { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // What the caller may do with this comment, so the client does not have to repeat the rules.
    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}
