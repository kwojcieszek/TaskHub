using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class AdminUserService : IAdminUserService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public AdminUserService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyCollection<AdminUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = _userManager.Users.AsNoTracking().ToList();
        var result = new List<AdminUserDto>();

        var lastLogins = await _dbContext.UserActivityLogs
            .AsNoTracking()
            .Where(l => l.Type == UserActivityType.Login && l.FailureReason == null && l.UserId != null)
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

        // Nobody can take the role from themselves, so the acting administrator always remains one
        // and the system is never left without an administrator.
        if (userId == _currentUser.UserId && !request.Roles.Contains(Roles.Admin))
        {
            throw new ConflictException("You cannot remove the administrator role from your own account.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRolesAsync(user, request.Roles);

        // Roles travel inside the access token, so the user's sessions end and the next sign-in picks up the new ones.
        user.TokenVersion++;
        await _userManager.UpdateAsync(user);

        return await MapToDtoAsync(user);
    }

    public async Task<AdminUserDto> SetLockoutAsync(Guid userId, SetUserLockoutRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        if (request.LockedOut && userId == _currentUser.UserId)
        {
            throw new ConflictException("You cannot lock your own account.");
        }

        user.LockoutEnabled = true;
        await _userManager.SetLockoutEndDateAsync(user, request.LockedOut ? DateTimeOffset.MaxValue : null);

        return await MapToDtoAsync(user);
    }

    public async Task<AdminUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.UserName : request.DisplayName,
            // The administrator knows this password, so the user replaces it at the first sign-in.
            MustChangePassword = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(user, Roles.User);

        return await MapToDtoAsync(user);
    }

    public async Task<AdminUserDto> ResetPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors.Select(e => e.Description));
        }

        // Existing sessions end, and the user replaces the assigned password at the next sign-in.
        user.MustChangePassword = true;
        user.TokenVersion++;
        await _userManager.UpdateAsync(user);

        return await MapToDtoAsync(user);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        if (userId == _currentUser.UserId)
        {
            throw new ConflictException("You cannot delete your own account.");
        }

        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors.Select(e => e.Description));
        }
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User not found.");
    }

    private async Task<AdminUserDto> MapToDtoAsync(User user)
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
