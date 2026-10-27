using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class UserDirectoryService : IUserDirectoryService
{
    private const int MinQueryLength = 2;
    private const int MaxQueryLength = 100;
    private const int MaxResults = 20;

    private readonly UserManager<User> _userManager;
    private readonly ICurrentUserService _currentUser;

    public UserDirectoryService(UserManager<User> userManager, ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyCollection<UserSummaryDto>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        var text = (query ?? string.Empty).Trim();

        // Without something to look for nothing is returned, so the directory cannot be read out as a list of accounts.
        if (text.Length < MinQueryLength || text.Length > MaxQueryLength)
        {
            return new List<UserSummaryDto>();
        }

        var isAdmin = _currentUser.IsInRole(Roles.Admin);
        var upper = text.ToUpperInvariant();
        var lower = text.ToLowerInvariant();

        // Both cases are compared because some databases only change the case of ASCII letters.
        var users = _userManager.Users.AsNoTracking().Where(u =>
            u.DisplayName.Contains(text)
            || u.DisplayName.ToUpper().Contains(upper)
            || u.DisplayName.ToLower().Contains(lower)
            // A login is found only when it is typed in full; administrators may also search by a part of it.
            || u.NormalizedUserName == upper
            || (isAdmin && u.NormalizedUserName != null && u.NormalizedUserName.Contains(upper)));

        var found = await users
            .OrderBy(u => u.DisplayName)
            .Take(MaxResults)
            .Select(u => new { u.Id, u.UserName, u.DisplayName })
            .ToListAsync(cancellationToken);

        return found
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                // Logins are what an attacker needs to guess passwords or lock accounts, so only administrators see them.
                UserName = isAdmin ? u.UserName : null,
                DisplayName = u.DisplayName
            })
            .ToList();
    }
}
