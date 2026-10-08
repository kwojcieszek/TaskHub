using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.ApiTokens;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class ApiTokenService : IApiTokenService
{
    private const int MaxTokensPerUser = 20;
    private const int SecretBytes = 32;
    private const int VisiblePrefixLength = 12;

    // The last use is shown to the owner; writing it on every request would cost a database write per call.
    private static readonly TimeSpan LastUsedPrecision = TimeSpan.FromMinutes(5);

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<User> _userManager;

    public ApiTokenService(IApplicationDbContext dbContext, ICurrentUserService currentUser, UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public async Task<IReadOnlyCollection<ApiTokenDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.ApiTokens
            .AsNoTracking()
            .Where(t => t.UserId == _currentUser.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens.Select(token => Map(token, new ApiTokenDto())).ToList();
    }

    public async Task<CreatedApiTokenDto> CreateAsync(CreateApiTokenRequest request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            throw new ValidationFailedException(new[] { "The token name is required." });
        }

        var existing = await _dbContext.ApiTokens.AsNoTracking().CountAsync(t => t.UserId == _currentUser.UserId, cancellationToken);

        if (existing >= MaxTokensPerUser)
        {
            throw new ConflictException($"You can have at most {MaxTokensPerUser} tokens. Revoke one you no longer use first.");
        }

        var secret = ApiTokenFormat.Prefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var now = DateTime.UtcNow;

        var token = new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Name = name,
            TokenHash = Hash(secret),
            Prefix = secret[..VisiblePrefixLength],
            CreatedAt = now,
            ExpiresAt = request.ExpiresInDays.HasValue ? now.AddDays(request.ExpiresInDays.Value) : null
        };

        _dbContext.ApiTokens.Add(token);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = Map(token, new CreatedApiTokenDto());
        created.Token = secret;

        return created;
    }

    public async Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        // A token of another user is answered like one that does not exist.
        var token = await _dbContext.ApiTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == _currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Token not found.");

        _dbContext.ApiTokens.Remove(token);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApiTokenIdentity?> AuthenticateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(ApiTokenFormat.Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var hash = Hash(token);
        var now = DateTime.UtcNow;

        var stored = await _dbContext.ApiTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored?.User is null || (stored.ExpiresAt.HasValue && stored.ExpiresAt.Value <= now))
        {
            return null;
        }

        var user = stored.User;

        // A locked account is closed for its tokens exactly as it is for its sessions.
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (!stored.LastUsedAt.HasValue || now - stored.LastUsedAt.Value > LastUsedPrecision)
        {
            await _dbContext.ApiTokens
                .Where(t => t.Id == stored.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(t => t.LastUsedAt, now), cancellationToken);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new ApiTokenIdentity
        {
            TokenId = stored.Id,
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            MustChangePassword = user.MustChangePassword,
            Roles = roles.ToList()
        };
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }

    // Timestamps are stored in UTC; marking the kind makes the API emit them with a "Z" suffix.
    private static TDto Map<TDto>(ApiToken token, TDto dto) where TDto : ApiTokenDto
    {
        dto.Id = token.Id;
        dto.Name = token.Name;
        dto.Prefix = token.Prefix;
        dto.CreatedAt = DateTime.SpecifyKind(token.CreatedAt, DateTimeKind.Utc);
        dto.ExpiresAt = token.ExpiresAt.HasValue ? DateTime.SpecifyKind(token.ExpiresAt.Value, DateTimeKind.Utc) : null;
        dto.LastUsedAt = token.LastUsedAt.HasValue ? DateTime.SpecifyKind(token.LastUsedAt.Value, DateTimeKind.Utc) : null;
        dto.Expired = token.ExpiresAt.HasValue && token.ExpiresAt.Value <= DateTime.UtcNow;

        return dto;
    }
}
