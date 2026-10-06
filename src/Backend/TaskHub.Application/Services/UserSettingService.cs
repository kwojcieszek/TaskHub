using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.UserSettings;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

public class UserSettingService : IUserSettingService
{
    private const int MaxKeyLength = 100;
    private const int MaxValueLength = 4000;

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public UserSettingService(IApplicationDbContext dbContext, ICurrentUserService currentUser, IMapper mapper)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<UserSettingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId)
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<UserSettingDto>>(settings);
    }

    public async Task<UserSettingDto> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId && s.Key == key, cancellationToken)
            ?? throw new NotFoundException("Setting not found.");

        return _mapper.Map<UserSettingDto>(setting);
    }

    public async Task<UserSettingDto> SetAsync(string key, SetUserSettingRequest request, CancellationToken cancellationToken = default)
    {
        var value = request.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            throw new ValidationFailedException(new[] { $"Setting key is required and cannot exceed {MaxKeyLength} characters." });
        }

        if (value.Length > MaxValueLength)
        {
            throw new ValidationFailedException(new[] { $"Setting value cannot exceed {MaxValueLength} characters." });
        }

        var setting = await FindAsync(key, cancellationToken);

        if (setting is null)
        {
            setting = new UserSetting
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Key = key
            };

            _dbContext.UserSettings.Add(setting);
        }

        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<UserSettingDto>(setting);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await FindAsync(key, cancellationToken)
            ?? throw new NotFoundException("Setting not found.");

        _dbContext.UserSettings.Remove(setting);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<UserSetting?> FindAsync(string key, CancellationToken cancellationToken)
    {
        return _dbContext.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId && s.Key == key, cancellationToken);
    }
}
