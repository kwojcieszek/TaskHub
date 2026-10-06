namespace TaskHub.Application.Dtos.UserGroups;

public class UserGroupDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int MemberCount { get; set; }
}
