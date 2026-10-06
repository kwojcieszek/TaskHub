using TaskHub.Domain.Entities;

namespace TaskHub.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles);

    string CreateTwoFactorToken(Guid userId);

    Guid? ValidateTwoFactorToken(string token);
}
