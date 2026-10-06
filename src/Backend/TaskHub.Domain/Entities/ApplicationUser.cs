using Microsoft.AspNetCore.Identity;

namespace TaskHub.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();

    public ICollection<UserGroupMember> GroupMemberships { get; set; } = new List<UserGroupMember>();
}
