using TaskHub.Application.Dtos.UserGroups;

namespace TaskHub.Application.Interfaces;

public interface IUserGroupService
{
    Task<IReadOnlyCollection<UserGroupDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserGroupDto> CreateAsync(CreateUserGroupRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserGroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<UserGroupMemberDto> AddMemberAsync(Guid groupId, AddUserGroupMemberRequest request, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
}
