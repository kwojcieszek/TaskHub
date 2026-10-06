namespace TaskHub.Domain.Entities;

public class UserGroupMember
{
    public Guid Id { get; set; }

    public Guid UserGroupId { get; set; }

    public UserGroup? UserGroup { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
