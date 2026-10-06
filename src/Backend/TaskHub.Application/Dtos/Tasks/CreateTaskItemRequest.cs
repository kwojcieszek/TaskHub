using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Tasks;

public class CreateTaskItemRequest
{
    public Guid? ParentTaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Medium;

    public Guid? AssignedUserId { get; set; }
}
