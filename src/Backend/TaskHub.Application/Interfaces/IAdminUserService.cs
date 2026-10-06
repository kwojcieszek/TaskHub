using TaskHub.Application.Dtos.Admin;

namespace TaskHub.Application.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyCollection<AdminUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    Task<AdminUserDto> UpdateRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);

    Task<AdminUserDto> SetLockoutAsync(Guid userId, SetUserLockoutRequest request, CancellationToken cancellationToken = default);

    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
