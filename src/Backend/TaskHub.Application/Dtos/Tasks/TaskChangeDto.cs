namespace TaskHub.Application.Dtos.Tasks;

public class TaskChangeDto
{
    public Guid Id { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
