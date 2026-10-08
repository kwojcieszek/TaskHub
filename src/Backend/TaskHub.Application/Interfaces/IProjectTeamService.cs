using TaskHub.Application.Dtos.Projects;

namespace TaskHub.Application.Interfaces;

public interface IProjectTeamService
{
    Task<IReadOnlyCollection<ProjectTeamDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProjectTeamDto>> GetAvailableAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectTeamDto> AddAsync(Guid projectId, AddProjectTeamRequest request, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid projectId, Guid userGroupId, CancellationToken cancellationToken = default);
}
