using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Admin;

public class UpdateUserRolesRequest
{
    [MaxLength(10)]
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}
