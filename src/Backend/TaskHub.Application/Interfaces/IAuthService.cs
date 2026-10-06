using TaskHub.Application.Dtos.Auth;

namespace TaskHub.Application.Interfaces;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<LoginResponse> VerifyTwoFactorAsync(TwoFactorVerifyRequest request, CancellationToken cancellationToken = default);

    Task<TwoFactorSetupResponse> BeginTwoFactorSetupAsync(Guid userId, CancellationToken cancellationToken = default);

    Task EnableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    Task DisableTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    AuthProvidersDto GetProviders();

    Task<LoginResponse> LoginWithGoogleAsync(GoogleTokenRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> LinkGoogleAsync(Guid userId, GoogleTokenRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> UnlinkGoogleAsync(Guid userId, CancellationToken cancellationToken = default);
}
