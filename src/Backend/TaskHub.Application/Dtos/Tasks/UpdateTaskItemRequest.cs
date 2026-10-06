using System.ComponentModel.DataAnnotations;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Tasks;

public class UpdateTaskItemRequest
{
    public Guid? ParentTaskId { get; set; }

    [Required]
    [StringLength(300)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    public TaskItemStatus Status { get; set; }

    public TaskItemPriority Priority { get; set; }

    public Guid? AssignedUserId { get; set; }
}
