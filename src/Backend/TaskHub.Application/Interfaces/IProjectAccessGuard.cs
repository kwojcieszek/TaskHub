namespace TaskHub.Application.Interfaces;

public interface IProjectAccessGuard
{
    Task EnsureCanViewAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task EnsureCanEditAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task EnsureCanManageAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Whether the caller manages the project, for rules that merely depend on it instead of requiring it.</summary>
    Task<bool> CanManageAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task EnsureCanManageGroupAsync(Guid projectGroupId, CancellationToken cancellationToken = default);
}
