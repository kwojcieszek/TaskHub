using TaskHub.Application.Dtos.UserSettings;

namespace TaskHub.Application.Interfaces;

public interface IUserSettingService
{
    Task<IReadOnlyCollection<UserSettingDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserSettingDto> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<UserSettingDto> SetAsync(string key, SetUserSettingRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
