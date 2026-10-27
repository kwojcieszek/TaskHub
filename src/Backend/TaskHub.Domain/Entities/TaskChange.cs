namespace TaskHub.Domain.Entities;

public class TaskChange
{
    public Guid Id { get; set; }
    public Guid TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
