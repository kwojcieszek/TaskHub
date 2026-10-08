using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Dtos.Common;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Interfaces;

public interface IAuditLogService
{
    Task RecordLoginAsync(Guid? userId, string userName, LoginFailureReason? failureReason, CancellationToken cancellationToken = default);

    Task RecordActivityAsync(Guid userId, string userName, string action, string httpMethod, string path, int statusCode, CancellationToken cancellationToken = default);

    Task<PagedResultDto<UserActivityLogDto>> GetLoginLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    Task<PagedResultDto<UserActivityLogDto>> GetLoginLogsForUserAsync(Guid userId, AuditLogQuery query, CancellationToken cancellationToken = default);

    Task<PagedResultDto<UserActivityLogDto>> GetActivityLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
