namespace TaskHub.Application.Dtos.Admin;

public class UserSummaryDto
{
    public Guid Id { get; set; }

    // Filled in for administrators only.
    public string? UserName { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}
