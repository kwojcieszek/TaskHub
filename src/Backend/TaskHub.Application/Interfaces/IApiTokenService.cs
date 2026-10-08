using TaskHub.Application.Dtos.ApiTokens;

namespace TaskHub.Application.Interfaces;

public interface IApiTokenService
{
    Task<IReadOnlyCollection<ApiTokenDto>> GetMineAsync(CancellationToken cancellationToken = default);

    Task<CreatedApiTokenDto> CreateAsync(CreateApiTokenRequest request, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default);

    Task<ApiTokenIdentity?> AuthenticateAsync(string token, CancellationToken cancellationToken = default);
}
