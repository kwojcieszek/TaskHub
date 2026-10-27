using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class ProjectService : IProjectService
{
    private const int MaxGitUrlLength = 500;
    private const int MaxIconImageLength = 150_000;

    private static readonly Regex IconPattern = new("^pi-[a-z0-9-]{1,40}$", RegexOptions.Compiled);
    private static readonly Regex IconImagePattern = new("^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/]+={0,2}$", RegexOptions.Compiled);

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IAttachmentService _attachmentService;
    private readonly IMapper _mapper;

    public ProjectService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IProjectAccessGuard accessGuard,
        IAttachmentService attachmentService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
        _attachmentService = attachmentService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<ProjectDto>> GetAccessibleProjectsAsync(Guid? projectGroupId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Project> query = _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.ProjectGroup)
            .Include(p => p.Tasks);

        if (projectGroupId.HasValue)
        {
            query = query.Where(p => p.ProjectGroupId == projectGroupId.Value);
        }

        if (!_currentUser.IsInRole(Roles.Admin))
        {
            query = query.Where(ProjectAccess.VisibleTo(_currentUser.UserId));
        }

        var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);

        return _mapper.Map<List<ProjectDto>>(projects);
    }

    public async Task<ProjectDto> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var project = await LoadProjectAsync(projectId, cancellationToken);

        return _mapper.Map<ProjectDto>(project);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProjectGroupId.HasValue)
        {
            await _accessGuard.EnsureCanManageGroupAsync(request.ProjectGroupId.Value, cancellationToken);
        }

        var icon = NormalizeIcon(request.Icon);
        var gitUrl = NormalizeGitUrl(request.GitUrl);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Icon = icon,
            GitUrl = gitUrl,
            ProjectGroupId = request.ProjectGroupId,
            OwnerId = _currentUser.UserId
        };

        project.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = _currentUser.UserId,
            Role = ProjectMemberRole.Owner
        });

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await LoadProjectAsync(project.Id, cancellationToken);

        return _mapper.Map<ProjectDto>(created);
    }

    public async Task<ProjectDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanEditAsync(projectId, cancellationToken);

        var icon = NormalizeIcon(request.Icon);
        var gitUrl = NormalizeGitUrl(request.GitUrl);

        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");

        // Switching a project on or off is a management decision, not an ordinary edit.
        if (request.IsActive != project.IsActive)
        {
            await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.Icon = icon;
        project.GitUrl = gitUrl;
        project.IsActive = request.IsActive;
        project.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await LoadProjectAsync(projectId, cancellationToken);

        return _mapper.Map<ProjectDto>(updated);
    }

    public async Task<ProjectDto> SetGroupAsync(Guid projectId, SetProjectGroupRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);

        if (request.ProjectGroupId.HasValue)
        {
            await _accessGuard.EnsureCanManageGroupAsync(request.ProjectGroupId.Value, cancellationToken);
        }

        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");

        project.ProjectGroupId = request.ProjectGroupId;
        project.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await LoadProjectAsync(projectId, cancellationToken);

        return _mapper.Map<ProjectDto>(updated);
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);

        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");

        var subTasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId && t.ParentTaskId != null)
            .ToListAsync(cancellationToken);

        var attachmentHashes = await _dbContext.Attachments
            .AsNoTracking()
            .Where(a => a.TaskItem!.ProjectId == projectId)
            .Select(a => a.Sha256)
            .ToListAsync(cancellationToken);

        var comments = await _dbContext.Comments
            .Where(c => c.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        _dbContext.Comments.RemoveRange(comments);
        _dbContext.Tasks.RemoveRange(subTasks);
        _dbContext.Projects.Remove(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _attachmentService.RemoveUnreferencedContentAsync(attachmentHashes, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectMemberDto>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanViewAsync(projectId, cancellationToken);

        var members = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var result = _mapper.Map<List<ProjectMemberDto>>(members);

        foreach (var member in result)
        {
            member.Direct = true;
        }

        await AddTeamMembersAsync(projectId, result, cancellationToken);

        return result;
    }

    // People who are in the project through a team are listed with the role the team gives them.
    private async Task AddTeamMembersAsync(Guid projectId, List<ProjectMemberDto> result, CancellationToken cancellationToken)
    {
        var teams = await _dbContext.ProjectTeams
            .AsNoTracking()
            .Include(t => t.UserGroup!)
            .ThenInclude(g => g.Members)
            .ThenInclude(m => m.User)
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.UserGroup!.Name)
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            foreach (var teamMember in team.UserGroup!.Members.Where(m => m.User is not null))
            {
                var entry = result.FirstOrDefault(member => member.UserId == teamMember.UserId);

                if (entry is null)
                {
                    entry = new ProjectMemberDto
                    {
                        UserId = teamMember.UserId,
                        UserName = teamMember.User!.UserName ?? string.Empty,
                        DisplayName = teamMember.User.DisplayName,
                        Role = team.Role.ToString()
                    };

                    result.Add(entry);
                }
                else if (Enum.TryParse<ProjectMemberRole>(entry.Role, out var current) && ProjectAccess.IsStronger(team.Role, current))
                {
                    entry.Role = team.Role.ToString();
                }

                entry.Teams.Add(team.UserGroup.Name);
            }
        }
    }

    public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);

        // A number outside the enum would be stored as a role none of the access checks knows.
        if (!await _dbContext.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
            throw new NotFoundException("User not found.");
        if (!Enum.IsDefined(request.Role))
        {
            throw new ValidationFailedException(new[] { "The role must be Owner, Editor or Viewer." });
        }

        var exists = await _dbContext.ProjectMembers
            .AsNoTracking()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == request.UserId, cancellationToken);

        if (exists)
        {
            throw new ConflictException("User is already a member of this project.");
        }

        var member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = request.UserId,
            Role = request.Role
        };

        _dbContext.ProjectMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Include(m => m.User)
            .FirstAsync(m => m.Id == member.Id, cancellationToken);

        return _mapper.Map<ProjectMemberDto>(created);
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanManageAsync(projectId, cancellationToken);

        var member = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Membership not found.");

        if (member.Role == ProjectMemberRole.Owner)
        {
            throw new ConflictException("The project owner cannot be removed.");
        }

        _dbContext.ProjectMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AssignmentCleanup.ClearInaccessibleAsync(_dbContext, new[] { projectId }, cancellationToken);
    }

    private static string? NormalizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        var trimmed = icon.Trim();

        if (trimmed.StartsWith("data:", StringComparison.Ordinal))
        {
            if (trimmed.Length > MaxIconImageLength || !IconImagePattern.IsMatch(trimmed))
            {
                throw new ValidationFailedException(new[] { $"Custom icon must be a PNG, JPEG or WebP image encoded as a data URL of at most {MaxIconImageLength} characters." });
            }

            return trimmed;
        }

        if (!IconPattern.IsMatch(trimmed))
        {
            throw new ValidationFailedException(new[] { "Icon must be a PrimeIcons name, for example 'pi-briefcase', or a custom image." });
        }

        return trimmed;
    }

    private static string? NormalizeGitUrl(string? gitUrl)
    {
        if (string.IsNullOrWhiteSpace(gitUrl))
        {
            return null;
        }

        var trimmed = gitUrl.Trim();

        var isHttpUrl = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        if (!isHttpUrl || trimmed.Length > MaxGitUrlLength)
        {
            throw new ValidationFailedException(new[] { $"Git URL must be an http(s) address of at most {MaxGitUrlLength} characters." });
        }

        return trimmed;
    }

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.ProjectGroup)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
    }
}
