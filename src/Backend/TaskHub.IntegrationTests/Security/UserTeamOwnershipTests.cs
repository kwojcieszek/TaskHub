using System.Net;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

public class UserTeamOwnershipTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public UserTeamOwnershipTests(TaskHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OwnerCanTransferToMemberAndMembersCanLeave()
    {
        var owner = await _factory.CreateUserAsync("transferowner");
        var member = await _factory.CreateUserAsync("newowner");
        var other = await _factory.CreateUserAsync("leavemember");
        var admin = await _factory.AdminAsync();
        var team = await owner.PostAsync<UserGroupDto>(ApiRoutes.UserGroups, new CreateUserGroupRequest { Name = "Transfer" });
        await owner.PostAsync<UserGroupMemberDto>(ApiRoutes.UserGroupMembers(team.Id), new AddUserGroupMemberRequest { UserId = member.UserId });
        await owner.PostAsync<UserGroupMemberDto>(ApiRoutes.UserGroupMembers(team.Id), new AddUserGroupMemberRequest { UserId = other.UserId });
        await (await owner.PostAsync(ApiRoutes.UserGroup(team.Id) + "/leave")).ShouldBeAsync(HttpStatusCode.Conflict);
        await (await other.PutAsync(ApiRoutes.UserGroup(team.Id) + "/owner", new TransferTeamOwnershipRequest { UserId = other.UserId })).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await admin.DeleteAsync(ApiRoutes.AdminUser(owner.UserId))).ShouldBeAsync(HttpStatusCode.Conflict);
        var transferred = await owner.PutAsync<UserGroupDto>(ApiRoutes.UserGroup(team.Id) + "/owner", new TransferTeamOwnershipRequest { UserId = member.UserId });
        Assert.Equal(member.UserId, transferred.OwnerId);
        await (await owner.PutAsync(ApiRoutes.UserGroup(team.Id), new UpdateUserGroupRequest { Name = "Hijack" })).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await other.PostAsync(ApiRoutes.UserGroup(team.Id) + "/leave")).ShouldBeAsync(HttpStatusCode.NoContent);
        Assert.DoesNotContain(await other.GetAsync<List<UserGroupDto>>(ApiRoutes.UserGroups), g => g.Id == team.Id);
    }

    [Fact]
    public async Task User_CanCreateAndManageTheirTeam_ButOthersCannot()
    {
        var owner = await _factory.CreateUserAsync("teamowner");
        var other = await _factory.CreateUserAsync("teammember");
        var stranger = await _factory.CreateUserAsync("outsider");
        var admin = await _factory.AdminAsync();
        var team = await owner.PostAsync<UserGroupDto>(ApiRoutes.UserGroups,
            new CreateUserGroupRequest { Name = "Owned team" });

        Assert.True(team.CanManage);
        Assert.Contains(await owner.GetAsync<List<UserGroupDto>>(ApiRoutes.UserGroups), t => t.Id == team.Id);
        Assert.DoesNotContain(await stranger.GetAsync<List<UserGroupDto>>(ApiRoutes.UserGroups), t => t.Id == team.Id);
        await (await stranger.GetAsync(ApiRoutes.UserGroupMembers(team.Id))).ShouldBeAsync(HttpStatusCode.Forbidden);

        await owner.PostAsync<UserGroupMemberDto>(ApiRoutes.UserGroupMembers(team.Id),
            new AddUserGroupMemberRequest { UserId = other.UserId });
        var visible = Assert.Single(await other.GetAsync<List<UserGroupDto>>(ApiRoutes.UserGroups), t => t.Id == team.Id);
        Assert.False(visible.CanManage);
        Assert.Single(await other.GetAsync<List<UserGroupMemberDto>>(ApiRoutes.UserGroupMembers(team.Id)));
        await (await other.PutAsync(ApiRoutes.UserGroup(team.Id), new UpdateUserGroupRequest { Name = "Hijacked" })).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await other.PostAsync(ApiRoutes.UserGroupMembers(team.Id), new AddUserGroupMemberRequest { UserId = stranger.UserId })).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await other.DeleteAsync(ApiRoutes.UserGroupMember(team.Id, other.UserId))).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await stranger.DeleteAsync(ApiRoutes.UserGroup(team.Id))).ShouldBeAsync(HttpStatusCode.Forbidden);

        var renamed = await owner.PutAsync<UserGroupDto>(ApiRoutes.UserGroup(team.Id), new UpdateUserGroupRequest { Name = "Renamed team" });
        Assert.Equal("Renamed team", renamed.Name);
        Assert.True((await admin.GetAsync<List<UserGroupDto>>(ApiRoutes.UserGroups)).Single(t => t.Id == team.Id).CanManage);
        await (await owner.DeleteAsync(ApiRoutes.UserGroupMember(team.Id, other.UserId))).ShouldBeAsync(HttpStatusCode.NoContent);
        await (await owner.DeleteAsync(ApiRoutes.UserGroup(team.Id))).ShouldBeAsync(HttpStatusCode.NoContent);
    }
}
