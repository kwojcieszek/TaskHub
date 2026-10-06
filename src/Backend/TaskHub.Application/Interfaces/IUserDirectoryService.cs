using TaskHub.Application.Dtos.Admin;

namespace TaskHub.Application.Interfaces;

public interface IUserDirectoryService
{
    Task<IReadOnlyCollection<UserSummaryDto>> SearchAsync(string? query, CancellationToken cancellationToken = default);
}
