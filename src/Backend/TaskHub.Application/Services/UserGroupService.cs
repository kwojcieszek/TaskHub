using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class UserGroupService : IUserGroupService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<User> _userManager;

    public UserGroupService(IApplicationDbContext dbContext, IMapper mapper, ICurrentUserService currentUser, UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public async Task<IReadOnlyCollection<UserGroupDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _dbContext.UserGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => _currentUser.IsInRole(Roles.Admin) || g.OwnerId == _currentUser.UserId || g.Members.Any(m => m.UserId == _currentUser.UserId))
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var ownerIds = groups.Where(g => g.OwnerId.HasValue).Select(g => g.OwnerId!.Value).Distinct().ToList();
        var owners = await _userManager.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
        return groups.Select(g => {
            var dto = MapGroup(g);
            dto.OwnerDisplayName = g.OwnerId.HasValue ? owners.GetValueOrDefault(g.OwnerId.Value) : null;
            return dto;
        }).ToList();
    }

    public async Task<UserGroupDto> CreateAsync(CreateUserGroupRequest request, CancellationToken cancellationToken = default)
    {
        var group = new UserGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            OwnerId = _currentUser.UserId
        };

        _dbContext.UserGroups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapGroup(group);
    }

    public async Task<UserGroupDto> UpdateAsync(Guid groupId, UpdateUserGroupRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(groupId, true, cancellationToken);
        var name = (request.Name ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            throw new ValidationFailedException(new[] { "The group name is required." });
        }

        var group = await _dbContext.UserGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new NotFoundException("User group not found.");

        group.Name = name;
        group.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapGroup(group);
    }

    public async Task DeleteAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(groupId, true, cancellationToken);
        var group = await _dbContext.UserGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new NotFoundException("User group not found.");

        var projectIds = await _dbContext.ProjectTeams.Where(t => t.UserGroupId == groupId).Select(t => t.ProjectId).ToListAsync(cancellationToken);
        _dbContext.UserGroups.Remove(group);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AssignmentCleanup.ClearInaccessibleAsync(_dbContext, projectIds, cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserGroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(groupId, false, cancellationToken);
        var members = await _dbContext.UserGroupMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.UserGroupId == groupId)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<UserGroupMemberDto>>(members);
    }

    public async Task<UserGroupMemberDto> AddMemberAsync(Guid groupId, AddUserGroupMemberRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(groupId, true, cancellationToken);
        if (!await _userManager.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            throw new NotFoundException("User not found.");
        }
        var exists = await _dbContext.UserGroupMembers
            .AsNoTracking()
            .AnyAsync(m => m.UserGroupId == groupId && m.UserId == request.UserId, cancellationToken);

        if (exists)
        {
            throw new ConflictException("User is already a member of this group.");
        }

        var member = new UserGroupMember
        {
            Id = Guid.NewGuid(),
            UserGroupId = groupId,
            UserId = request.UserId
        };

        _dbContext.UserGroupMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.UserGroupMembers
            .AsNoTracking()
            .Include(m => m.User)
            .FirstAsync(m => m.Id == member.Id, cancellationToken);

        return _mapper.Map<UserGroupMemberDto>(created);
    }

    public async Task RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(groupId, true, cancellationToken);
        var member = await _dbContext.UserGroupMembers
            .FirstOrDefaultAsync(m => m.UserGroupId == groupId && m.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Membership not found.");

        _dbContext.UserGroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await ClearInaccessibleAssignmentsAsync(groupId, userId, cancellationToken);
    }

    private UserGroupDto MapGroup(UserGroup group)
    {
        var dto = _mapper.Map<UserGroupDto>(group);
        dto.CanManage = _currentUser.IsInRole(Roles.Admin) || group.OwnerId == _currentUser.UserId;
        return dto;
    }

    public async Task<UserGroupDto> TransferOwnershipAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        await EnsureAccessAsync(groupId, true, ct);
        if (!await _userManager.Users.AnyAsync(u => u.Id == userId, ct))
            throw new NotFoundException("User not found.");
        if (!await _dbContext.UserGroupMembers.AnyAsync(m => m.UserGroupId == groupId && m.UserId == userId, ct))
            throw new ValidationFailedException(new[] { "The new owner must be a member of the team." });
        var group = await _dbContext.UserGroups.Include(g => g.Members).FirstAsync(g => g.Id == groupId, ct);
        group.OwnerId = userId;
        await _dbContext.SaveChangesAsync(ct);
        return MapGroup(group);
    }

    public async Task LeaveAsync(Guid groupId, CancellationToken ct = default)
    {
        await EnsureAccessAsync(groupId, false, ct);
        var group = await _dbContext.UserGroups.AsNoTracking().FirstAsync(g => g.Id == groupId, ct);
        if (group.OwnerId == _currentUser.UserId)
            throw new ConflictException("Transfer team ownership before leaving.");
        var member = await _dbContext.UserGroupMembers.FirstOrDefaultAsync(m => m.UserGroupId == groupId && m.UserId == _currentUser.UserId, ct)
            ?? throw new NotFoundException("Membership not found.");
        _dbContext.UserGroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync(ct);
        await ClearInaccessibleAssignmentsAsync(groupId, _currentUser.UserId, ct);
    }

    private async Task ClearInaccessibleAssignmentsAsync(Guid groupId, Guid userId, CancellationToken ct)
    {
        var projects = await _dbContext.ProjectTeams.Where(t => t.UserGroupId == groupId).Select(t => t.ProjectId).ToListAsync(ct);
        await AssignmentCleanup.ClearInaccessibleAsync(_dbContext, projects, ct);
    }

    private async Task EnsureAccessAsync(Guid groupId, bool manage, CancellationToken cancellationToken)
    {
        var group = await _dbContext.UserGroups.AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => new { g.OwnerId, IsMember = g.Members.Any(m => m.UserId == _currentUser.UserId) })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User group not found.");

        if (!_currentUser.IsInRole(Roles.Admin) && group.OwnerId != _currentUser.UserId && (manage || !group.IsMember))
        {
            throw new ForbiddenAccessException("You do not have permission to access this team.");
        }
    }
}
