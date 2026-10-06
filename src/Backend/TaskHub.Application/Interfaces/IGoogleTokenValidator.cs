using TaskHub.Application.Dtos.Auth;

namespace TaskHub.Application.Interfaces;

public interface IGoogleTokenValidator
{
    string? ClientId { get; }

    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
