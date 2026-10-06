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

        var isMember = await _dbContext.ProjectMembers
            .AsNoTracking()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == _currentUser.UserId, cancellationToken);

        if (!isMember)
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

        var role = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == _currentUser.UserId)
            .Select(m => (ProjectMemberRole?)m.Role)
            .FirstOrDefaultAsync(cancellationToken);

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

        var role = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == _currentUser.UserId)
            .Select(m => (ProjectMemberRole?)m.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (role != ProjectMemberRole.Owner)
        {
            throw new ForbiddenAccessException("Only the project owner can perform this action.");
        }
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
