namespace TaskHub.Application.Dtos.UserGroups;

public class CreateUserGroupRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
