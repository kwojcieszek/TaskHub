using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class AdminUserService : IAdminUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _dbContext;

    public AdminUserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = _userManager.Users.AsNoTracking().ToList();
        var result = new List<AdminUserDto>();

        var lastLogins = await _dbContext.LoginLogs
            .AsNoTracking()
            .Where(l => l.Succeeded && l.UserId != null)
            .GroupBy(l => l.UserId!.Value)
            .Select(g => new { UserId = g.Key, LastLoginAt = g.Max(l => l.CreatedAt) })
            .ToDictionaryAsync(l => l.UserId, l => l.LastLoginAt, cancellationToken);

        foreach (var user in users)
        {
            var dto = await MapToDtoAsync(user);

            if (lastLogins.TryGetValue(user.Id, out var lastLoginAt))
            {
                dto.LastLoginAt = DateTime.SpecifyKind(lastLoginAt, DateTimeKind.Utc);
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<AdminUserDto> UpdateRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        foreach (var roleName in request.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                throw new ValidationFailedException(new[] { $"Role '{roleName}' does not exist." });
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRolesAsync(user, request.Roles);

        return await MapToDtoAsync(user);
    }

    public async Task<AdminUserDto> SetLockoutAsync(Guid userId, SetUserLockoutRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        user.LockoutEnabled = true;
        await _userManager.SetLockoutEndDateAsync(user, request.LockedOut ? DateTimeOffset.MaxValue : null);

        return await MapToDtoAsync(user);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors.Select(e => e.Description));
        }
    }

    private async Task<ApplicationUser> GetUserOrThrowAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User not found.");
    }

    private async Task<AdminUserDto> MapToDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var lockedOut = await _userManager.IsLockedOutAsync(user);

        return new AdminUserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            LockedOut = lockedOut,
            TwoFactorEnabled = user.TwoFactorEnabled,
            Roles = roles.ToList()
        };
    }
}
