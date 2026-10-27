using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class ProjectTeamService : IProjectTeamService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;

    public ProjectTeamService(IApplicationDbContext dbContext, IProjectAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
    }

    public async Task<IReadOnlyCollection<ProjectTeamDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        return await _dbContext.ProjectTeams
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.UserGroup!.Name)
            .Select(t => new ProjectTeamDto
            {
                UserGroupId = t.UserGroupId,
                Name = t.UserGroup!.Name,
                Description = t.UserGroup.Description,
                MemberCount = t.UserGroup.Members.Count,
                Role = t.Role.ToString()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectTeamDto>> GetAvailableAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        // Only someone who may share the project gets to see which teams can be added.
        await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);

        return await _dbContext.UserGroups
            .AsNoTracking()
            .Where(g => !_dbContext.ProjectTeams.Any(t => t.ProjectId == projectId && t.UserGroupId == g.Id))
            .OrderBy(g => g.Name)
            .Select(g => new ProjectTeamDto
            {
                UserGroupId = g.Id,
                Name = g.Name,
                Description = g.Description,
                MemberCount = g.Members.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectTeamDto> AddAsync(Guid projectId, AddProjectTeamRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);

        // A project has one owner who answers for it; a whole team is never given that role.
        if (request.Role != ProjectMemberRole.Editor && request.Role != ProjectMemberRole.Viewer)
        {
            throw new ValidationFailedException(new[] { "A team can be added as Editor or Viewer." });
        }

        var groupExists = await _dbContext.UserGroups.AsNoTracking().AnyAsync(g => g.Id == request.UserGroupId, cancellationToken);

        if (!groupExists)
        {
            throw new NotFoundException("Team not found.");
        }

        var alreadyAdded = await _dbContext.ProjectTeams
            .AsNoTracking()
            .AnyAsync(t => t.ProjectId == projectId && t.UserGroupId == request.UserGroupId, cancellationToken);

        if (alreadyAdded)
        {
            throw new ConflictException("The team already has access to this project.");
        }

        _dbContext.ProjectTeams.Add(new ProjectTeam
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserGroupId = request.UserGroupId,
            Role = request.Role
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var teams = await GetAsync(projectId, cancellationToken);

        return teams.First(team => team.UserGroupId == request.UserGroupId);
    }

    public async Task RemoveAsync(Guid projectId, Guid userGroupId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);

        var team = await _dbContext.ProjectTeams
            .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.UserGroupId == userGroupId, cancellationToken)
            ?? throw new NotFoundException("The team has no access to this project.");

        _dbContext.ProjectTeams.Remove(team);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AssignmentCleanup.ClearInaccessibleAsync(_dbContext, new[] { projectId }, cancellationToken);
    }
}
