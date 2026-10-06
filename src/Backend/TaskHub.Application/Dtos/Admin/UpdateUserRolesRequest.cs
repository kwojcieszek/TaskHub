namespace TaskHub.Application.Dtos.Admin;

public class UpdateUserRolesRequest
{
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}
