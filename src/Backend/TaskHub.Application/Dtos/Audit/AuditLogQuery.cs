namespace TaskHub.Application.Dtos.Audit;

public class AuditLogQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? Search { get; set; }
}
