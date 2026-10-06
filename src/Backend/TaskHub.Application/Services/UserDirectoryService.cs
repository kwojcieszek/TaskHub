using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class UserDirectoryService : IUserDirectoryService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserDirectoryService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public Task<IReadOnlyCollection<UserSummaryDto>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        var users = _userManager.Users.AsNoTracking().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            users = users.Where(u =>
                (u.UserName != null && u.UserName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                u.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var result = users
            .Take(20)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                DisplayName = u.DisplayName
            })
            .ToList();

        return Task.FromResult<IReadOnlyCollection<UserSummaryDto>>(result);
    }
}
