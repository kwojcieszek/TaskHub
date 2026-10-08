using TaskHub.Application.Common;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Application.Interfaces;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class UserGroupServiceTests : IDisposable
{
    private readonly TestHost _host = new();
    private readonly IUserGroupService _groups;

    public UserGroupServiceTests()
    {
        _groups = _host.Get<IUserGroupService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Create_AddsAnEmptyGroup_ListedByName()
    {
        await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Testers", Description = "QA" });
        await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Developers" });

        var groups = await _groups.GetAllAsync();

        Assert.Equal(new[] { "Developers", "Testers" }, groups.Select(group => group.Name));
        Assert.All(groups, group => Assert.Equal(0, group.MemberCount));
        Assert.Equal("QA", groups.Last().Description);
    }

    [Fact]
    public async Task Update_RenamesTheGroupAndKeepsItsMembers()
    {
        var user = await _host.CreateUserAsync("anna");
        var group = await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Developers", Description = "Old" });
        await _groups.AddMemberAsync(group.Id, new AddUserGroupMemberRequest { UserId = user.Id });

        var updated = await _groups.UpdateAsync(group.Id, new UpdateUserGroupRequest { Name = "  Engineers  ", Description = "  " });
        var listed = Assert.Single(await _groups.GetAllAsync());

        Assert.Equal(group.Id, updated.Id);
        Assert.Equal("Engineers", updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal(1, updated.MemberCount);
        Assert.Equal("Engineers", listed.Name);
        Assert.Equal(user.Id, Assert.Single(await _groups.GetMembersAsync(group.Id)).UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_RejectsABlankName_AndAMissingGroup(string name)
    {
        var group = await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Developers" });

        await Assert.ThrowsAsync<ValidationFailedException>(() => _groups.UpdateAsync(group.Id, new UpdateUserGroupRequest { Name = name }));
        await Assert.ThrowsAsync<NotFoundException>(() => _groups.UpdateAsync(Guid.NewGuid(), new UpdateUserGroupRequest { Name = "Ghosts" }));
        Assert.Equal("Developers", Assert.Single(await _groups.GetAllAsync()).Name);
    }

    [Fact]
    public async Task Update_ShowsTheNewNameInTheProjectsTheTeamBelongsTo()
    {
        var owner = await _host.CreateUserAsync("owner");
        var member = await _host.CreateUserAsync("member");
        var project = await _host.CreateProjectAsync(owner);
        var team = await _host.CreateTeamAsync("Developers", member);
        await _host.AddTeamAsync(project, owner, team, TaskHub.Domain.Enums.ProjectMemberRole.Editor);

        await _groups.UpdateAsync(team.Id, new UpdateUserGroupRequest { Name = "Engineers" });

        var teams = await _host.Get<IProjectTeamService>().GetAsync(project.Id);
        var members = await _host.Get<IProjectService>().GetMembersAsync(project.Id);

        Assert.Equal("Engineers", Assert.Single(teams).Name);
        Assert.Equal(new[] { "Engineers" }, members.Single(entry => entry.UserId == member.Id).Teams);
    }

    [Fact]
    public async Task AddMember_PutsTheUserInTheGroupOnce()
    {
        var user = await _host.CreateUserAsync("anna");
        var group = await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Developers" });

        var added = await _groups.AddMemberAsync(group.Id, new AddUserGroupMemberRequest { UserId = user.Id });

        await Assert.ThrowsAsync<ConflictException>(() => _groups.AddMemberAsync(group.Id, new AddUserGroupMemberRequest { UserId = user.Id }));
        Assert.Equal(user.Id, added.UserId);
        Assert.Equal("anna", added.UserName);
        Assert.Equal(user.Id, Assert.Single(await _groups.GetMembersAsync(group.Id)).UserId);
        Assert.Equal(1, Assert.Single(await _groups.GetAllAsync()).MemberCount);
    }

    [Fact]
    public async Task RemoveMember_TakesTheUserOut_AndFailsForSomeoneWhoIsNotIn()
    {
        var user = await _host.CreateUserAsync("anna");
        var other = await _host.CreateUserAsync("bartek");
        var group = await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Developers" });
        await _groups.AddMemberAsync(group.Id, new AddUserGroupMemberRequest { UserId = user.Id });

        await _groups.RemoveMemberAsync(group.Id, user.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _groups.RemoveMemberAsync(group.Id, user.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _groups.RemoveMemberAsync(group.Id, other.Id));
        Assert.Empty(await _groups.GetMembersAsync(group.Id));
    }

    [Fact]
    public async Task Delete_RemovesTheGroupWithItsMemberships_ButNotTheUsers()
    {
        var user = await _host.CreateUserAsync("anna");
        var group = await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Developers" });
        await _groups.AddMemberAsync(group.Id, new AddUserGroupMemberRequest { UserId = user.Id });

        await _groups.DeleteAsync(group.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _groups.DeleteAsync(group.Id));
        Assert.Empty(await _groups.GetAllAsync());
        Assert.Equal(0, _host.Db.UserGroupMembers.Count());
        Assert.NotNull(await _host.Users.FindByIdAsync(user.Id.ToString()));
    }

    [Fact]
    public async Task DeletingAUser_TakesThemOutOfTheirGroups()
    {
        var user = await _host.CreateUserAsync("anna");
        var group = await _groups.CreateAsync(new CreateUserGroupRequest { Name = "Developers" });
        await _groups.AddMemberAsync(group.Id, new AddUserGroupMemberRequest { UserId = user.Id });

        await _host.Users.DeleteAsync(user);

        Assert.Empty(await _groups.GetMembersAsync(group.Id));
        Assert.Single(await _groups.GetAllAsync());
    }
}
