using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Tasks;

public class TaskItemDto
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? ParentTaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    public TaskItemStatus Status { get; set; }

    public TaskItemPriority Priority { get; set; }

    public Guid? AssignedUserId { get; set; }

    public string? AssignedUserDisplayName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int AttachmentCount { get; set; }

    public IReadOnlyCollection<TaskItemDto> SubTasks { get; set; } = new List<TaskItemDto>();
}
