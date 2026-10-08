using TaskHub.Application.Dtos.Comments;

namespace TaskHub.Application.Interfaces;

public interface ICommentService
{
    Task<IReadOnlyCollection<CommentDto>> GetForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentDto>> GetForTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    Task<CommentDto> AddToProjectAsync(Guid projectId, SaveCommentRequest request, CancellationToken cancellationToken = default);

    Task<CommentDto> AddToTaskAsync(Guid projectId, Guid taskId, SaveCommentRequest request, CancellationToken cancellationToken = default);

    Task<CommentDto> UpdateAsync(Guid projectId, Guid commentId, SaveCommentRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, Guid commentId, CancellationToken cancellationToken = default);
}
