namespace TaskHub.Application.Dtos.Common;

public class PagedResultDto<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
