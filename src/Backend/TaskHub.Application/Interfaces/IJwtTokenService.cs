using TaskHub.Domain.Entities;

namespace TaskHub.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user, IReadOnlyCollection<string> roles, Guid? sessionId = null);

    string CreateTwoFactorToken(Guid userId);

    Guid? ValidateTwoFactorToken(string token);
}
