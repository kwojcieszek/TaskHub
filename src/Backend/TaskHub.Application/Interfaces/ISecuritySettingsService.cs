using TaskHub.Application.Dtos.Security;

namespace TaskHub.Application.Interfaces;

public interface ISecuritySettingsService
{
    Task<SecuritySettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<SecuritySettingsDto> UpdateAsync(SecuritySettingsDto request, CancellationToken cancellationToken = default);
}
