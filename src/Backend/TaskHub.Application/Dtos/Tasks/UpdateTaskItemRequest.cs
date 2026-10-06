using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Tasks;

public class UpdateTaskItemRequest
{
    public Guid? ParentTaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    public TaskItemStatus Status { get; set; }

    public TaskItemPriority Priority { get; set; }

    public Guid? AssignedUserId { get; set; }
}
