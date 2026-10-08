using TaskHub.Application.Common;
using TaskHub.Domain.Enums;

namespace TaskHub.Tests.Common;

public class ProjectAccessTests
{
    [Fact]
    public void Strongest_OfNoRoles_IsNone()
    {
        Assert.Null(ProjectAccess.Strongest(Array.Empty<ProjectMemberRole>()));
    }

    [Theory]
    [InlineData(new[] { ProjectMemberRole.Viewer }, ProjectMemberRole.Viewer)]
    [InlineData(new[] { ProjectMemberRole.Viewer, ProjectMemberRole.Editor }, ProjectMemberRole.Editor)]
    [InlineData(new[] { ProjectMemberRole.Editor, ProjectMemberRole.Viewer }, ProjectMemberRole.Editor)]
    [InlineData(new[] { ProjectMemberRole.Editor, ProjectMemberRole.Owner, ProjectMemberRole.Viewer }, ProjectMemberRole.Owner)]
    [InlineData(new[] { ProjectMemberRole.Viewer, ProjectMemberRole.Viewer }, ProjectMemberRole.Viewer)]
    public void Strongest_IsTheRoleThatAllowsTheMost(ProjectMemberRole[] roles, ProjectMemberRole expected)
    {
        Assert.Equal(expected, ProjectAccess.Strongest(roles));
    }

    [Theory]
    [InlineData(ProjectMemberRole.Owner, ProjectMemberRole.Editor, true)]
    [InlineData(ProjectMemberRole.Owner, ProjectMemberRole.Viewer, true)]
    [InlineData(ProjectMemberRole.Editor, ProjectMemberRole.Viewer, true)]
    [InlineData(ProjectMemberRole.Editor, ProjectMemberRole.Owner, false)]
    [InlineData(ProjectMemberRole.Viewer, ProjectMemberRole.Editor, false)]
    [InlineData(ProjectMemberRole.Viewer, ProjectMemberRole.Viewer, false)]
    [InlineData(ProjectMemberRole.Owner, ProjectMemberRole.Owner, false)]
    public void IsStronger_ComparesWhatTheRolesAllow(ProjectMemberRole role, ProjectMemberRole than, bool expected)
    {
        Assert.Equal(expected, ProjectAccess.IsStronger(role, than));
    }

    [Fact]
    public void UndefinedRole_IsWeakerThanEveryRealOne()
    {
        var undefined = (ProjectMemberRole)99;

        Assert.False(ProjectAccess.IsStronger(undefined, ProjectMemberRole.Viewer));
        Assert.True(ProjectAccess.IsStronger(ProjectMemberRole.Viewer, undefined));
        Assert.Equal(ProjectMemberRole.Viewer, ProjectAccess.Strongest(new[] { undefined, ProjectMemberRole.Viewer }));
    }
}
