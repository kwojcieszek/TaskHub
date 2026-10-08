using System.Linq.Expressions;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Common;

/// <summary>
/// The one definition of who can open a project: its members, and the members of the teams it is shared with.
/// Every list and every check uses it, so they cannot disagree.
/// </summary>
public static class ProjectAccess
{
    public static Expression<Func<Project, bool>> VisibleTo(Guid userId)
    {
        return project => project.Members.Any(member => member.UserId == userId)
            || project.Teams.Any(team => team.UserGroup!.Members.Any(member => member.UserId == userId));
    }

    /// <summary>The role that allows the most among the given ones, or none when there are none.</summary>
    public static ProjectMemberRole? Strongest(IEnumerable<ProjectMemberRole> roles)
    {
        ProjectMemberRole? strongest = null;

        foreach (var role in roles)
        {
            if (strongest is null || IsStronger(role, strongest.Value))
            {
                strongest = role;
            }
        }

        return strongest;
    }

    public static bool IsStronger(ProjectMemberRole role, ProjectMemberRole than)
    {
        return Rank(role) > Rank(than);
    }

    private static int Rank(ProjectMemberRole role)
    {
        return role switch
        {
            ProjectMemberRole.Owner => 3,
            ProjectMemberRole.Editor => 2,
            ProjectMemberRole.Viewer => 1,
            _ => 0
        };
    }
}
