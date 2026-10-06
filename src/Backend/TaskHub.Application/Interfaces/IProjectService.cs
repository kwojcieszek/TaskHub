using TaskHub.Application.Dtos.Projects;

namespace TaskHub.Application.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyCollection<ProjectDto>> GetAccessibleProjectsAsync(Guid? projectGroupId = null, CancellationToken cancellationToken = default);

    Task<ProjectDto> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);

    Task<ProjectDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    Task<ProjectDto> SetGroupAsync(Guid projectId, SetProjectGroupRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProjectMemberDto>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
