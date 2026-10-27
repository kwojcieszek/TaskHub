using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class TaskItemService : ITaskItemService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IAttachmentService _attachmentService;
    private readonly IMapper _mapper;

    public TaskItemService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IProjectAccessGuard accessGuard,
        IAttachmentService attachmentService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
        _attachmentService = attachmentService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<TaskItemDto>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var tasks = await _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedUser)
            .Include(t => t.Attachments)
            .Include(t => t.SubTasks)
                .ThenInclude(s => s.AssignedUser)
            .Include(t => t.SubTasks)
                .ThenInclude(s => s.Attachments)
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<TaskItemDto>>(tasks);
    }

    public async Task<IReadOnlyCollection<TaskChangeDto>> GetHistoryAsync(Guid projectId, Guid taskId, int page = 1, CancellationToken ct = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, ct);
        if (!await _dbContext.Tasks.AnyAsync(t => t.Id == taskId && t.ProjectId == projectId, ct))
            throw new NotFoundException("Task not found.");
        if (page < 1 || page > 10000) throw new ValidationFailedException(new[] { "Invalid history page." });
        var changes = await _dbContext.TaskChanges.AsNoTracking().Where(c => c.TaskItemId == taskId && c.ProjectId == projectId)
            .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id).Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        return changes.Select(c => new TaskChangeDto { Id = c.Id, ActorName = c.ActorName, Field = c.Field,
            OldValue = c.OldValue, NewValue = c.NewValue, ChangedAt = DateTime.SpecifyKind(c.ChangedAt, DateTimeKind.Utc) }).ToList();
    }

    public async Task<IReadOnlyCollection<TaskItemDto>> GetSubTasksAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var task = await LoadTaskAsync(projectId, taskId, cancellationToken);

        return _mapper.Map<TaskItemDto>(task).SubTasks;
    }

    public async Task<TaskItemDto> GetByIdAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var task = await LoadTaskAsync(projectId, taskId, cancellationToken);

        return _mapper.Map<TaskItemDto>(task);
    }

    public async Task<TaskItemDto> CreateAsync(Guid projectId, CreateTaskItemRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);

        if (request.ParentTaskId.HasValue)
        {
            await EnsureCanBeParentAsync(projectId, request.ParentTaskId.Value, cancellationToken);
        }

        if (request.AssignedUserId.HasValue)
        {
            await EnsureCanBeAssignedAsync(projectId, request.AssignedUserId.Value, cancellationToken);
        }

        EnsureCanRepeat(request.ParentTaskId, request.Recurrence);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParentTaskId = request.ParentTaskId,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Priority = request.Priority,
            AssignedUserId = request.AssignedUserId
        };

        SetRecurrence(task, request.Recurrence);

        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncParentStatusAsync(task.ParentTaskId, cancellationToken);

        var created = await LoadTaskAsync(projectId, task.Id, cancellationToken);

        return _mapper.Map<TaskItemDto>(created);
    }

    public async Task<TaskItemDto> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskItemRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);

        var task = await _dbContext.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");

        var previousParentTaskId = task.ParentTaskId;
        var previousStatus = task.Status;
        var hasSubTasks = await HasSubTasksAsync(taskId, cancellationToken);

        if (request.ParentTaskId.HasValue && request.ParentTaskId != task.ParentTaskId)
        {
            if (request.ParentTaskId.Value == taskId)
            {
                throw new ValidationFailedException(new[] { "A task cannot be its own parent." });
            }

            if (hasSubTasks)
            {
                throw new ValidationFailedException(new[] { "A task with subtasks cannot become a subtask." });
            }

            await EnsureCanBeParentAsync(projectId, request.ParentTaskId.Value, cancellationToken);
        }

        // Only a new assignee is checked, so a task stays editable after its assignee left the project.
        if (request.AssignedUserId.HasValue && request.AssignedUserId != task.AssignedUserId)
        {
            await EnsureCanBeAssignedAsync(projectId, request.AssignedUserId.Value, cancellationToken);
        }

        EnsureCanRepeat(request.ParentTaskId, request.Recurrence);

        task.ParentTaskId = request.ParentTaskId;
        task.Title = request.Title;
        task.Description = request.Description;
        task.DueDate = request.DueDate;
        task.Priority = request.Priority;
        task.AssignedUserId = request.AssignedUserId;
        task.UpdatedAt = DateTime.UtcNow;
        SetRecurrence(task, request.Recurrence);

        // A task with subtasks takes its status from them, so the requested status is ignored.
        if (!hasSubTasks)
        {
            ApplyStatus(task, request.Status);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncParentStatusAsync(task.ParentTaskId, cancellationToken);

        if (previousParentTaskId != task.ParentTaskId)
        {
            await SyncParentStatusAsync(previousParentTaskId, cancellationToken);
        }

        await RepeatIfCompletedAsync(task, previousStatus, cancellationToken);

        var updated = await LoadTaskAsync(projectId, taskId, cancellationToken);

        return _mapper.Map<TaskItemDto>(updated);
    }

    public async Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);

        var task = await _dbContext.Tasks
            .Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");

        var attachmentHashes = await _dbContext.Attachments
            .AsNoTracking()
            .Where(a => a.TaskItemId == taskId || a.TaskItem!.ParentTaskId == taskId)
            .Select(a => a.Sha256)
            .ToListAsync(cancellationToken);

        var comments = await _dbContext.Comments
            .Where(c => c.TaskItemId == taskId || c.TaskItem!.ParentTaskId == taskId)
            .ToListAsync(cancellationToken);

        _dbContext.Comments.RemoveRange(comments);
        _dbContext.Tasks.RemoveRange(task.SubTasks);
        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncParentStatusAsync(task.ParentTaskId, cancellationToken);

        await _attachmentService.RemoveUnreferencedContentAsync(attachmentHashes, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TaskItemDto>> GetCalendarAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedUser)
            .Include(t => t.Attachments)
            .Where(t => t.Project!.IsActive && t.DueDate != null && t.DueDate >= from && t.DueDate <= to);

        if (!_currentUser.IsInRole(Roles.Admin))
        {
            var visibleProjects = VisibleProjectIds();
            query = query.Where(t => visibleProjects.Contains(t.ProjectId));
        }

        var tasks = await query.OrderBy(t => t.DueDate).ToListAsync(cancellationToken);

        return _mapper.Map<List<TaskItemDto>>(tasks);
    }

    public async Task<IReadOnlyCollection<TaskItemDto>> GetMyTasksAsync(CancellationToken cancellationToken = default)
    {
        // Tasks of deactivated projects stay out of the cross-project lists; they remain visible inside the project.
        var query = _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedUser)
            .Include(t => t.Attachments)
            .Where(t => t.Project!.IsActive);

        if (!_currentUser.IsInRole(Roles.Admin))
        {
            var visibleProjects = VisibleProjectIds();
            query = query.Where(t => visibleProjects.Contains(t.ProjectId));
        }

        var tasks = await query
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<TaskItemDto>>(tasks);
    }

    public async Task<TaskItemDto> StartAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);

        var task = await _dbContext.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");

        await EnsureHasNoSubTasksAsync(taskId, cancellationToken);

        if (task.Status != TaskItemStatus.ToDo)
        {
            throw new ValidationFailedException(new[] { "Only a task that has not been started yet can be started." });
        }

        task.Status = TaskItemStatus.InProgress;
        task.StartedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncParentStatusAsync(task.ParentTaskId, cancellationToken);

        var updated = await LoadTaskAsync(projectId, taskId, cancellationToken);

        return _mapper.Map<TaskItemDto>(updated);
    }

    public async Task<TaskItemDto> CompleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);

        var task = await _dbContext.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");

        await EnsureHasNoSubTasksAsync(taskId, cancellationToken);

        if (task.Status == TaskItemStatus.Done)
        {
            throw new ValidationFailedException(new[] { "The task is already completed." });
        }

        var previousStatus = task.Status;

        // A task may be completed without being started; StartedAt then simply stays empty.
        task.Status = TaskItemStatus.Done;
        task.CompletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncParentStatusAsync(task.ParentTaskId, cancellationToken);
        await RepeatIfCompletedAsync(task, previousStatus, cancellationToken);

        var updated = await LoadTaskAsync(projectId, taskId, cancellationToken);

        return _mapper.Map<TaskItemDto>(updated);
    }

    private async Task<TaskItem> LoadTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        return await _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedUser)
            .Include(t => t.Attachments)
            .Include(t => t.SubTasks)
                .ThenInclude(s => s.AssignedUser)
            .Include(t => t.SubTasks)
                .ThenInclude(s => s.Attachments)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
    }

    private static void ApplyStatus(TaskItem task, TaskItemStatus status)
    {
        if (status == TaskItemStatus.Done && task.Status != TaskItemStatus.Done)
        {
            task.CompletedAt = DateTime.UtcNow;
        }
        else if (status != TaskItemStatus.Done)
        {
            task.CompletedAt = null;
        }

        // The start time follows the status too: set when work begins, cleared when the task goes back to "to do".
        if (status == TaskItemStatus.InProgress && task.StartedAt is null)
        {
            task.StartedAt = DateTime.UtcNow;
        }
        else if (status == TaskItemStatus.ToDo)
        {
            task.StartedAt = null;
        }

        task.Status = status;
    }

    private async Task<bool> HasSubTasksAsync(Guid taskId, CancellationToken cancellationToken)
    {
        return await _dbContext.Tasks.AsNoTracking().AnyAsync(t => t.ParentTaskId == taskId, cancellationToken);
    }

    private async Task EnsureHasNoSubTasksAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (await HasSubTasksAsync(taskId, cancellationToken))
        {
            throw new ValidationFailedException(new[] { "A task with subtasks is started and completed through its subtasks." });
        }
    }

    // A task with subtasks has no status of its own: it is derived from the subtasks after each change to them.
    // Cancelled subtasks do not count; the task is done once every remaining subtask is done.
    private async Task SyncParentStatusAsync(Guid? parentTaskId, CancellationToken cancellationToken)
    {
        if (!parentTaskId.HasValue)
        {
            return;
        }

        var subTasks = await _dbContext.Tasks
            .AsNoTracking()
            .Where(t => t.ParentTaskId == parentTaskId)
            .Select(t => new { t.Status, t.StartedAt, t.CompletedAt })
            .ToListAsync(cancellationToken);

        // Without subtasks it is an ordinary task again and keeps the status it had.
        if (subTasks.Count == 0)
        {
            return;
        }

        var parent = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == parentTaskId, cancellationToken);

        if (parent is null)
        {
            return;
        }

        var active = subTasks.Where(s => s.Status != TaskItemStatus.Cancelled).ToList();
        TaskItemStatus status;

        if (active.Count == 0)
        {
            status = TaskItemStatus.Cancelled;
        }
        else if (active.All(s => s.Status == TaskItemStatus.Done))
        {
            status = TaskItemStatus.Done;
        }
        else if (active.All(s => s.Status == TaskItemStatus.ToDo))
        {
            status = TaskItemStatus.ToDo;
        }
        else
        {
            status = TaskItemStatus.InProgress;
        }

        var isRunning = status == TaskItemStatus.InProgress || status == TaskItemStatus.Done;
        DateTime? startedAt = isRunning ? active.Min(s => s.StartedAt) : null;
        DateTime? completedAt = status == TaskItemStatus.Done ? active.Max(s => s.CompletedAt) ?? DateTime.UtcNow : null;

        if (parent.Status == status && parent.StartedAt == startedAt && parent.CompletedAt == completedAt)
        {
            return;
        }

        var previousStatus = parent.Status;

        parent.Status = status;
        parent.StartedAt = startedAt;
        parent.CompletedAt = completedAt;
        parent.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await RepeatIfCompletedAsync(parent, previousStatus, cancellationToken);
    }

    private static void EnsureCanRepeat(Guid? parentTaskId, TaskRecurrenceDto? recurrence)
    {
        if (recurrence is null)
        {
            return;
        }

        if (parentTaskId.HasValue)
        {
            throw new ValidationFailedException(new[] { "Only a top-level task can repeat; its subtasks are repeated with it." });
        }

        if (recurrence.Interval is < TaskRecurrence.MinInterval or > TaskRecurrence.MaxInterval ||
            !Enum.IsDefined(recurrence.Unit) || !Enum.IsDefined(recurrence.Basis))
        {
            throw new ValidationFailedException(new[] { $"A task repeats every {TaskRecurrence.MinInterval} to {TaskRecurrence.MaxInterval} days, weeks, months or years." });
        }
    }

    private static void SetRecurrence(TaskItem task, TaskRecurrenceDto? recurrence)
    {
        task.RecurrenceInterval = recurrence?.Interval;
        task.RecurrenceUnit = recurrence?.Unit;
        task.RecurrenceBasis = recurrence?.Basis;
    }

    private static TaskRecurrenceDto? GetRecurrence(TaskItem task)
    {
        if (!task.RecurrenceInterval.HasValue || !task.RecurrenceUnit.HasValue)
        {
            return null;
        }

        return new TaskRecurrenceDto
        {
            Interval = task.RecurrenceInterval.Value,
            Unit = task.RecurrenceUnit.Value,
            Basis = task.RecurrenceBasis ?? RecurrenceBasis.CompletionDate
        };
    }

    // Completing a repeating task creates its next occurrence, with fresh copies of the subtasks.
    // The schedule moves to the new task, so reopening and completing the old one does not repeat it twice.
    private async Task RepeatIfCompletedAsync(TaskItem task, TaskItemStatus previousStatus, CancellationToken cancellationToken)
    {
        var recurrence = GetRecurrence(task);

        if (recurrence is null || task.ParentTaskId.HasValue || previousStatus == TaskItemStatus.Done || task.Status != TaskItemStatus.Done)
        {
            return;
        }

        var nextDueDate = TaskRecurrence.NextDueDate(recurrence, task.DueDate, task.CompletedAt ?? DateTime.UtcNow);
        var shift = task.DueDate.HasValue ? nextDueDate - task.DueDate.Value : (TimeSpan?)null;

        var next = CopyAsToDo(task, nextDueDate, null);
        next.AssignedUserId = await KeepAssigneeAsync(task, cancellationToken);
        SetRecurrence(next, recurrence);
        _dbContext.Tasks.Add(next);

        var subTasks = await _dbContext.Tasks
            .AsNoTracking()
            .Where(t => t.ParentTaskId == task.Id)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var subTask in subTasks)
        {
            var copy = CopyAsToDo(subTask, subTask.DueDate + shift, next.Id);
            copy.AssignedUserId = await KeepAssigneeAsync(subTask, cancellationToken);
            _dbContext.Tasks.Add(copy);
        }

        SetRecurrence(task, null);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TaskItem CopyAsToDo(TaskItem source, DateTime? dueDate, Guid? parentTaskId)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = source.ProjectId,
            ParentTaskId = parentTaskId,
            Title = source.Title,
            Description = source.Description,
            DueDate = dueDate,
            Priority = source.Priority
        };
    }

    // An assignee who has left the project in the meantime is not carried over to the next occurrence.
    private async Task<Guid?> KeepAssigneeAsync(TaskItem source, CancellationToken cancellationToken)
    {
        if (!source.AssignedUserId.HasValue)
        {
            return null;
        }

        return await IsProjectMemberAsync(source.ProjectId, source.AssignedUserId.Value, cancellationToken)
            ? source.AssignedUserId
            : null;
    }

    // The projects the caller can open, as a subquery for the lists that span projects.
    private IQueryable<Guid> VisibleProjectIds()
    {
        return _dbContext.Projects.Where(ProjectAccess.VisibleTo(_currentUser.UserId)).Select(p => p.Id);
    }

    // The assignee is shown to everyone in the project, so it has to be someone who can open it.
    private async Task EnsureCanBeAssignedAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsProjectMemberAsync(projectId, userId, cancellationToken))
        {
            throw new ValidationFailedException(new[] { "A task can only be assigned to a member of the project." });
        }
    }

    private async Task<bool> IsProjectMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .AnyAsync(ProjectAccess.VisibleTo(userId), cancellationToken);
    }

    private async Task EnsureCanBeParentAsync(Guid projectId, Guid parentTaskId, CancellationToken cancellationToken)
    {
        var parent = await _dbContext.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == parentTaskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Parent task not found.");

        if (parent.ParentTaskId.HasValue)
        {
            throw new ValidationFailedException(new[] { "A subtask cannot have its own subtasks." });
        }
    }
}
