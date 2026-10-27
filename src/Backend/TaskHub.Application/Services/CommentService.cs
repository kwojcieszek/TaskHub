using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Comments;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

/// <summary>
/// Comments on a project and on its tasks. Everyone who can open the project reads them and may add their own;
/// a comment is changed only by its author and removed by its author or by whoever manages the project.
/// </summary>
public class CommentService : ICommentService
{
    // The newest comments are returned; a longer history is not something the page can show at once anyway.
    private const int MaxComments = 500;

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessGuard _accessGuard;

    public CommentService(IApplicationDbContext dbContext, ICurrentUserService currentUser, IProjectAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
    }

    public async Task<IReadOnlyCollection<CommentDto>> GetForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        return await ListAsync(projectId, null, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommentDto>> GetForTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);
        await EnsureTaskExistsAsync(projectId, taskId, cancellationToken);

        return await ListAsync(projectId, taskId, cancellationToken);
    }

    public async Task<CommentDto> AddToProjectAsync(Guid projectId, SaveCommentRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        return await AddAsync(projectId, null, request, cancellationToken);
    }

    public async Task<CommentDto> AddToTaskAsync(Guid projectId, Guid taskId, SaveCommentRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);
        await EnsureTaskExistsAsync(projectId, taskId, cancellationToken);

        return await AddAsync(projectId, taskId, request, cancellationToken);
    }

    public async Task<CommentDto> UpdateAsync(Guid projectId, Guid commentId, SaveCommentRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var content = Normalize(request.Content);
        var comment = await FindAsync(projectId, commentId, cancellationToken);

        // Nobody puts words into someone else's mouth, not even an administrator.
        if (comment.AuthorId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("Only the author can change a comment.");
        }

        comment.Content = content;
        comment.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await LoadAsync(comment.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid commentId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var comment = await FindAsync(projectId, commentId, cancellationToken);

        if (comment.AuthorId != _currentUser.UserId && !await _accessGuard.CanManageAsync(projectId, cancellationToken))
        {
            throw new ForbiddenAccessException("Only the author or the project owner can delete a comment.");
        }

        _dbContext.Comments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CommentDto> AddAsync(Guid projectId, Guid? taskId, SaveCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TaskItemId = taskId,
            AuthorId = _currentUser.UserId,
            Content = Normalize(request.Content)
        };

        _dbContext.Comments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await LoadAsync(comment.Id, cancellationToken);
    }

    private async Task<IReadOnlyCollection<CommentDto>> ListAsync(Guid projectId, Guid? taskId, CancellationToken cancellationToken)
    {
        var comments = await _dbContext.Comments
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.ProjectId == projectId && c.TaskItemId == taskId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(MaxComments)
            .ToListAsync(cancellationToken);

        var canManage = await _accessGuard.CanManageAsync(projectId, cancellationToken);

        return comments
            .OrderBy(c => c.CreatedAt)
            .Select(comment => Map(comment, canManage))
            .ToList();
    }

    private async Task<CommentDto> LoadAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await _dbContext.Comments
            .AsNoTracking()
            .Include(c => c.Author)
            .FirstAsync(c => c.Id == commentId, cancellationToken);

        return Map(comment, await _accessGuard.CanManageAsync(comment.ProjectId, cancellationToken));
    }

    // The project comes from the address: a comment of another project is answered like one that does not exist.
    private async Task<Comment> FindAsync(Guid projectId, Guid commentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Comment not found.");
    }

    private async Task EnsureTaskExistsAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Tasks.AsNoTracking().AnyAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Task not found.");
        }
    }

    private static string Normalize(string? content)
    {
        var text = (content ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            throw new ValidationFailedException(new[] { "The comment cannot be empty." });
        }

        return text;
    }

    // Timestamps are stored in UTC; marking the kind makes the API emit them with a "Z" suffix.
    private CommentDto Map(Comment comment, bool canManage)
    {
        var isAuthor = comment.AuthorId == _currentUser.UserId;

        return new CommentDto
        {
            Id = comment.Id,
            ProjectId = comment.ProjectId,
            TaskItemId = comment.TaskItemId,
            AuthorId = comment.AuthorId,
            AuthorDisplayName = comment.Author?.DisplayName,
            Content = comment.Content,
            CreatedAt = DateTime.SpecifyKind(comment.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = comment.UpdatedAt.HasValue ? DateTime.SpecifyKind(comment.UpdatedAt.Value, DateTimeKind.Utc) : null,
            CanEdit = isAuthor,
            CanDelete = isAuthor || canManage
        };
    }
}
