using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class ProjectGroupService : IProjectGroupService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IMapper _mapper;

    public ProjectGroupService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IProjectAccessGuard accessGuard,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<ProjectGroupDto>> GetAccessibleGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await AccessibleGroups()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<ProjectGroupDto>>(groups);
    }

    public async Task<ProjectGroupDto> GetByIdAsync(Guid projectGroupId, CancellationToken cancellationToken = default)
    {
        var group = await AccessibleGroups()
            .FirstOrDefaultAsync(g => g.Id == projectGroupId, cancellationToken)
            ?? throw new NotFoundException("Project group not found.");

        return _mapper.Map<ProjectGroupDto>(group);
    }

    public async Task<ProjectGroupDto> CreateAsync(CreateProjectGroupRequest request, CancellationToken cancellationToken = default)
    {
        var group = new ProjectGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            OwnerId = _currentUser.UserId
        };

        _dbContext.ProjectGroups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(group.Id, cancellationToken);
    }

    public async Task<ProjectGroupDto> UpdateAsync(Guid projectGroupId, UpdateProjectGroupRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageGroupAsync(projectGroupId, cancellationToken);

        var group = await _dbContext.ProjectGroups.FirstOrDefaultAsync(g => g.Id == projectGroupId, cancellationToken)
            ?? throw new NotFoundException("Project group not found.");

        group.Name = request.Name;
        group.Description = request.Description;
        group.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(projectGroupId, cancellationToken);
    }

    public async Task DeleteAsync(Guid projectGroupId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageGroupAsync(projectGroupId, cancellationToken);

        var group = await _dbContext.ProjectGroups
            .Include(g => g.Projects)
            .FirstOrDefaultAsync(g => g.Id == projectGroupId, cancellationToken)
            ?? throw new NotFoundException("Project group not found.");

        _dbContext.ProjectGroups.Remove(group);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ProjectGroup> AccessibleGroups()
    {
        IQueryable<ProjectGroup> query = _dbContext.ProjectGroups
            .AsNoTracking()
            .Include(g => g.Owner)
            .Include(g => g.Projects);

        if (!_currentUser.IsInRole(Roles.Admin))
        {
            query = query.Where(g => g.OwnerId == _currentUser.UserId
                || g.Projects.Any(p => p.Members.Any(m => m.UserId == _currentUser.UserId)));
        }

        return query;
    }
}
