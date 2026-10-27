using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class ProjectAccessGuard : IProjectAccessGuard
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ProjectAccessGuard(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task EnsureCanViewAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsInRole(Roles.Admin))
        {
            return;
        }

        var role = await FindRoleAsync(projectId, cancellationToken);

        if (role is null)
        {
            throw new ForbiddenAccessException("You do not have access to this project.");
        }
    }

    public async Task EnsureCanEditAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsInRole(Roles.Admin))
        {
            return;
        }

        var role = await FindRoleAsync(projectId, cancellationToken);

        if (role is null || role == ProjectMemberRole.Viewer)
        {
            throw new ForbiddenAccessException("You do not have edit access to this project.");
        }
    }

    public async Task EnsureCanManageAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsInRole(Roles.Admin))
        {
            return;
        }

        var role = await FindRoleAsync(projectId, cancellationToken);

        if (role != ProjectMemberRole.Owner)
        {
            throw new ForbiddenAccessException("Only the project owner can perform this action.");
        }
    }

    public async Task<bool> CanManageAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsInRole(Roles.Admin))
        {
            return true;
        }

        return await FindRoleAsync(projectId, cancellationToken) == ProjectMemberRole.Owner;
    }

    // The strongest role the caller has in the project: their own membership or one of their teams'.
    private async Task<ProjectMemberRole?> FindRoleAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var roles = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => m.Role)
            .ToListAsync(cancellationToken);

        var teamRoles = await _dbContext.ProjectTeams
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.UserGroup!.Members.Any(m => m.UserId == userId))
            .Select(t => t.Role)
            .ToListAsync(cancellationToken);

        return ProjectAccess.Strongest(roles.Concat(teamRoles));
    }

    public async Task EnsureCanManageGroupAsync(Guid projectGroupId, CancellationToken cancellationToken = default)
    {
        var ownerId = await _dbContext.ProjectGroups
            .AsNoTracking()
            .Where(g => g.Id == projectGroupId)
            .Select(g => (Guid?)g.OwnerId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Project group not found.");

        if (!_currentUser.IsInRole(Roles.Admin) && ownerId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("Only the project group owner can perform this action.");
        }
    }
}
