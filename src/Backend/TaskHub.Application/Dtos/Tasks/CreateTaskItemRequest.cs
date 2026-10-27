using System.ComponentModel.DataAnnotations;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Tasks;

public class CreateTaskItemRequest
{
    public Guid? ParentTaskId { get; set; }

    [Required]
    [StringLength(300)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Medium;

    public Guid? AssignedUserId { get; set; }

    // Null for a task that does not repeat.
    public TaskRecurrenceDto? Recurrence { get; set; }
}
