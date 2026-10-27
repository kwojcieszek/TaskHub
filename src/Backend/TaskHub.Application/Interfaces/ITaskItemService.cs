using TaskHub.Application.Dtos.Tasks;

namespace TaskHub.Application.Interfaces;

public interface ITaskItemService
{
    Task<IReadOnlyCollection<TaskChangeDto>> GetHistoryAsync(Guid projectId, Guid taskId, int page = 1, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskItemDto>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskItemDto>> GetSubTasksAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskItemDto> GetByIdAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskItemDto> CreateAsync(Guid projectId, CreateTaskItemRequest request, CancellationToken cancellationToken = default);

    Task<TaskItemDto> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskItemRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskItemDto>> GetCalendarAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskItemDto>> GetMyTasksAsync(CancellationToken cancellationToken = default);

    Task<TaskItemDto> StartAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskItemDto> CompleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
}
