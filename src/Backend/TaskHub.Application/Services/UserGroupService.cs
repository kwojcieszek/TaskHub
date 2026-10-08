using AutoMapper;
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

    public UserGroupService(IApplicationDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<UserGroupDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _dbContext.UserGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<UserGroupDto>>(groups);
    }

    public async Task<UserGroupDto> CreateAsync(CreateUserGroupRequest request, CancellationToken cancellationToken = default)
    {
        var group = new UserGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description
        };

        _dbContext.UserGroups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<UserGroupDto>(group);
    }

    public async Task<UserGroupDto> UpdateAsync(Guid groupId, UpdateUserGroupRequest request, CancellationToken cancellationToken = default)
    {
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

        return _mapper.Map<UserGroupDto>(group);
    }

    public async Task DeleteAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.UserGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new NotFoundException("User group not found.");

        _dbContext.UserGroups.Remove(group);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserGroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var members = await _dbContext.UserGroupMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.UserGroupId == groupId)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<UserGroupMemberDto>>(members);
    }

    public async Task<UserGroupMemberDto> AddMemberAsync(Guid groupId, AddUserGroupMemberRequest request, CancellationToken cancellationToken = default)
    {
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
        var member = await _dbContext.UserGroupMembers
            .FirstOrDefaultAsync(m => m.UserGroupId == groupId && m.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Membership not found.");

        _dbContext.UserGroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
