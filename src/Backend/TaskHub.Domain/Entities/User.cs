using Microsoft.AspNetCore.Identity;

namespace TaskHub.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set while the user signs in with a password someone else chose (the default administrator's, or one set by an administrator).
    public bool MustChangePassword { get; set; }

    // Raised whenever the user's existing sessions must stop working; access tokens carry the value they were issued with.
    public int TokenVersion { get; set; }

    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();

    public ICollection<UserGroupMember> GroupMemberships { get; set; } = new List<UserGroupMember>();
}
