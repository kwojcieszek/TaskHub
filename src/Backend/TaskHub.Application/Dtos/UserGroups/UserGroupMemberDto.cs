namespace TaskHub.Application.Dtos.UserGroups;

public class UserGroupMemberDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
