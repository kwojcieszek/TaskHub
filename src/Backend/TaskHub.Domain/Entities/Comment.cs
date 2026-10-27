namespace TaskHub.Domain.Entities;

public class Comment
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    // Empty for a comment on the project itself.
    public Guid? TaskItemId { get; set; }

    public TaskItem? TaskItem { get; set; }

    // Empty once the author's account has been deleted; the comment stays.
    public Guid? AuthorId { get; set; }

    public User? Author { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
