using TaskHub.Application.Dtos.ProjectGroups;

namespace TaskHub.Application.Interfaces;

public interface IProjectGroupService
{
    Task<IReadOnlyCollection<ProjectGroupDto>> GetAccessibleGroupsAsync(CancellationToken cancellationToken = default);

    Task<ProjectGroupDto> GetByIdAsync(Guid projectGroupId, CancellationToken cancellationToken = default);

    Task<ProjectGroupDto> CreateAsync(CreateProjectGroupRequest request, CancellationToken cancellationToken = default);

    Task<ProjectGroupDto> UpdateAsync(Guid projectGroupId, UpdateProjectGroupRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectGroupId, CancellationToken cancellationToken = default);
}
