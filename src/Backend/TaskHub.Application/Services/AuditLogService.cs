using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Dtos.Common;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class AuditLogService : IAuditLogService
{
    private const int MaxPageSize = 200;
    private const int MaxUserNameLength = 256;
    private const int MaxActionLength = 200;
    private const int MaxPathLength = 500;
    private const int MaxUserAgentLength = 500;

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task RecordLoginAsync(Guid? userId, string userName, LoginFailureReason? failureReason, CancellationToken cancellationToken = default)
    {
        var userAgent = _currentUser.UserAgent;

        var log = new LoginLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = Truncate(userName, MaxUserNameLength),
            IpAddress = _currentUser.IpAddress,
            UserAgent = userAgent is null ? null : Truncate(userAgent, MaxUserAgentLength),
            Succeeded = failureReason is null,
            FailureReason = failureReason
        };

        try
        {
            _dbContext.LoginLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Auditing must never decide whether a user can log in.
            _logger.LogWarning(exception, "Could not record a login attempt for {UserName}", log.UserName);
        }
    }

    public async Task RecordActivityAsync(Guid userId, string userName, string action, string httpMethod, string path, int statusCode, CancellationToken cancellationToken = default)
    {
        var log = new UserActivityLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = Truncate(userName, MaxUserNameLength),
            Action = Truncate(action, MaxActionLength),
            HttpMethod = httpMethod,
            Path = Truncate(path, MaxPathLength),
            StatusCode = statusCode,
            IpAddress = _currentUser.IpAddress
        };

        _dbContext.UserActivityLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PagedResultDto<LoginLogDto>> GetLoginLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        return GetLoginLogsAsync(_dbContext.LoginLogs.AsNoTracking(), query, cancellationToken);
    }

    public Task<PagedResultDto<LoginLogDto>> GetLoginLogsForUserAsync(Guid userId, AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        return GetLoginLogsAsync(_dbContext.LoginLogs.AsNoTracking().Where(l => l.UserId == userId), query, cancellationToken);
    }

    private async Task<PagedResultDto<LoginLogDto>> GetLoginLogsAsync(IQueryable<LoginLog> logs, AuditLogQuery query, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            logs = logs.Where(l => l.UserName.Contains(search) || (l.IpAddress != null && l.IpAddress.Contains(search)));
        }

        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var totalCount = await logs.CountAsync(cancellationToken);

        var items = await logs
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<LoginLogDto>
        {
            Items = _mapper.Map<List<LoginLogDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResultDto<UserActivityLogDto>> GetActivityLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = _dbContext.UserActivityLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            logs = logs.Where(l => l.UserName.Contains(search)
                || l.Action.Contains(search)
                || l.Path.Contains(search)
                || (l.IpAddress != null && l.IpAddress.Contains(search)));
        }

        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var totalCount = await logs.CountAsync(cancellationToken);

        var items = await logs
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<UserActivityLogDto>
        {
            Items = _mapper.Map<List<UserActivityLogDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static int NormalizePage(int page)
    {
        return Math.Max(page, 1);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, MaxPageSize);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
