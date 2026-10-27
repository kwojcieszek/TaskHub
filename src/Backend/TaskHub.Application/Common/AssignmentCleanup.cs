using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Interfaces;
namespace TaskHub.Application.Common;

public static class AssignmentCleanup
{
    public static async Task ClearInaccessibleAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> projectIds, CancellationToken ct)
    {
        var tasks = await db.Tasks.Where(t => projectIds.Contains(t.ProjectId) && t.AssignedUserId != null).ToListAsync(ct);
        foreach (var userId in tasks.Select(t => t.AssignedUserId!.Value).Distinct())
        {
            var visible = await db.Projects.Where(ProjectAccess.VisibleTo(userId)).Where(p => projectIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
            foreach (var task in tasks.Where(t => t.AssignedUserId == userId && !visible.Contains(t.ProjectId))) task.AssignedUserId = null;
        }
        await db.SaveChangesAsync(ct);
    }
}
