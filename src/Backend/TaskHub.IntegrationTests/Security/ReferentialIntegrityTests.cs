using System.Net;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Domain.Enums;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Requests that point at something that does not exist, or remove something others depend on:
/// the answer is a clear 4xx (or the operation succeeds cleanly), never an unhandled error or a broken system.
/// </summary>
public class ReferentialIntegrityTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public ReferentialIntegrityTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(Skip = KnownDefects.MissingUserCausesServerError)]
    public async Task AddingAProjectMember_WhoDoesNotExist_IsRefused()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);

        var response = await scenario.Cast.Owner.PostAsync(ApiRoutes.ProjectMembers(scenario.Project.Id), new AddProjectMemberRequest { UserId = Guid.NewGuid(), Role = ProjectMemberRole.Editor });

        await AssertClientErrorAsync(response);
    }

    [Theory]
    [InlineData("\"SuperOwner\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("[\"Owner\"]")]
    public async Task AddingAProjectMember_WithAnUnknownRole_IsRefused(string role)
    {
        await AssertRoleIsRefusedAsync(role);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("-1")]
    [InlineData("3")]
    public async Task AddingAProjectMember_WithAnUndefinedNumericRole_IsRefused(string role)
    {
        await AssertRoleIsRefusedAsync(role);
    }

    private async Task AssertRoleIsRefusedAsync(string role)
    {
        // A role outside Owner/Editor/Viewer is not covered by the access checks, so it must never be stored.
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var body = $"{{\"userId\":\"{scenario.Cast.Stranger.UserId}\",\"role\":{role}}}";

        var response = await scenario.Cast.Owner.Client.PostAsync(ApiRoutes.ProjectMembers(scenario.Project.Id), new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);

        var members = await scenario.Cast.Owner.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(scenario.Project.Id));
        Assert.DoesNotContain(members, member => member.UserId == scenario.Cast.Stranger.UserId);
    }

    [Fact]
    public async Task AssigningATask_ToAUserWhoDoesNotExist_IsRefused()
    {
        var scenario = await ProjectScenario.CreateAsync(_factory);
        var owner = scenario.Cast.Owner;

        var create = await owner.PostAsync(ApiRoutes.Tasks(scenario.Project.Id), new CreateTaskItemRequest { Title = "Task", AssignedUserId = Guid.NewGuid() });
        var update = await owner.PutAsync(ApiRoutes.Task(scenario.Project.Id, scenario.LeafTask.Id), new UpdateTaskItemRequest { Title = "Task", AssignedUserId = Guid.NewGuid() });

        await AssertClientErrorAsync(create);
        await AssertClientErrorAsync(update);
    }

    [Fact]
    public async Task AssigningATask_ToSomeoneOutsideTheProject_IsRefused()
    {
        // The assignee's name is shown to every member, and the assignee would be named on a project they cannot open.
        var scenario = await ProjectScenario.CreateAsync(_factory);

        var response = await scenario.Cast.Owner.PostAsync(ApiRoutes.Tasks(scenario.Project.Id), new CreateTaskItemRequest { Title = "Task", AssignedUserId = scenario.Cast.Stranger.UserId });

        await AssertClientErrorAsync(response);
    }

    [Fact(Skip = KnownDefects.MissingUserCausesServerError)]
    public async Task UserGroupMember_ForAMissingGroupOrUser_IsRefused()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();
        var group = await admin.PostAsync<UserGroupDto>(ApiRoutes.UserGroups, new CreateUserGroupRequest { Name = $"Group {Guid.NewGuid():N}" });

        var missingGroup = await admin.PostAsync(ApiRoutes.UserGroupMembers(Guid.NewGuid()), new AddUserGroupMemberRequest { UserId = user.UserId });
        var missingUser = await admin.PostAsync(ApiRoutes.UserGroupMembers(group.Id), new AddUserGroupMemberRequest { UserId = Guid.NewGuid() });

        await AssertClientErrorAsync(missingGroup);
        await AssertClientErrorAsync(missingUser);
    }

    [Fact(Skip = KnownDefects.MissingUserGroupIsNotReported)]
    public async Task UserGroupMembers_OfAMissingGroup_AreNotFound()
    {
        var admin = await _factory.AdminAsync();

        var response = await admin.GetAsync(ApiRoutes.UserGroupMembers(Guid.NewGuid()));

        await response.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MissingResources_AreNotFoundForAnAdministrator()
    {
        var admin = await _factory.AdminAsync();
        var missing = Guid.NewGuid();

        var responses = new[]
        {
            await admin.GetAsync(ApiRoutes.Project(missing)),
            await admin.PutAsync(ApiRoutes.Project(missing), new UpdateProjectRequest { Name = "Missing" }),
            await admin.DeleteAsync(ApiRoutes.Project(missing)),
            await admin.GetAsync(ApiRoutes.Task(missing, missing)),
            await admin.DeleteAsync(ApiRoutes.Task(missing, missing)),
            await admin.GetAsync(ApiRoutes.Attachments(missing, missing)),
            await admin.GetAsync(ApiRoutes.ProjectGroup(missing)),
            await admin.DeleteAsync(ApiRoutes.ProjectGroup(missing)),
            await admin.DeleteAsync(ApiRoutes.UserGroup(missing)),
            await admin.DeleteAsync(ApiRoutes.AdminUser(missing)),
            await admin.PutAsync(ApiRoutes.AdminUserRoles(missing), new UpdateUserRolesRequest { Roles = new[] { Roles.User } }),
            await admin.PutAsync(ApiRoutes.AdminUserLockout(missing), new SetUserLockoutRequest { LockedOut = true }),
            await admin.PutAsync(ApiRoutes.AdminUserPassword(missing), new ResetUserPasswordRequest { NewPassword = TaskHubApiFactory.AssignedPassword })
        };

        foreach (var response in responses)
        {
            await response.ShouldBeAsync(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task UpdatingRoles_WithAnUnknownRole_IsRefusedAndChangesNothing()
    {
        var admin = await _factory.AdminAsync();
        var user = await _factory.CreateUserAsync();

        var response = await admin.PutAsync(ApiRoutes.AdminUserRoles(user.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.User, "SuperAdmin" } });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);

        // Neither the roles nor the session of the user changed.
        var me = await user.GetAsync(ApiRoutes.Me);
        await me.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletingAUser_WhoIsOnlyAMember_RemovesTheirMemberships()
    {
        var admin = await _factory.AdminAsync();
        var owner = await _factory.CreateUserAsync("owner");
        var member = await _factory.CreateUserAsync("member");
        var project = await owner.CreateProjectAsync();
        await owner.AddMemberAsync(project.Id, member.UserId, ProjectMemberRole.Editor);
        var task = await owner.PostAsync<TaskItemDto>(ApiRoutes.Tasks(project.Id), new CreateTaskItemRequest { Title = "Assigned", AssignedUserId = member.UserId });

        var delete = await admin.DeleteAsync(ApiRoutes.AdminUser(member.UserId));
        await delete.ShouldBeAsync(HttpStatusCode.NoContent);

        var members = await owner.GetAsync<List<ProjectMemberDto>>(ApiRoutes.ProjectMembers(project.Id));
        var reloadedTask = await owner.GetAsync<TaskItemDto>(ApiRoutes.Task(project.Id, task.Id));

        Assert.Equal(owner.UserId, Assert.Single(members).UserId);
        Assert.Null(reloadedTask.AssignedUserId);
    }

    [Fact(Skip = KnownDefects.DeletingUserWithOwnedDataCausesServerError)]
    public async Task DeletingAUser_WhoOwnsAProject_IsHandledCleanly()
    {
        // Either the deletion is refused with an explanation, or it succeeds; it must not end in an unhandled error.
        var admin = await _factory.AdminAsync();
        var owner = await _factory.CreateUserAsync("owner");
        await owner.CreateProjectAsync();

        var response = await admin.DeleteAsync(ApiRoutes.AdminUser(owner.UserId));

        await AssertHandledAsync(response);
    }

    [Fact(Skip = KnownDefects.DeletingUserWithOwnedDataCausesServerError)]
    public async Task DeletingAUser_WhoUploadedAttachmentsToSomeoneElsesProject_IsHandledCleanly()
    {
        var admin = await _factory.AdminAsync();
        var owner = await _factory.CreateUserAsync("owner");
        var uploader = await _factory.CreateUserAsync("uploader");
        var project = await owner.CreateProjectAsync();
        var task = await owner.CreateTaskAsync(project.Id);
        await owner.AddMemberAsync(project.Id, uploader.UserId, ProjectMemberRole.Editor);
        await uploader.UploadAttachmentAsync(project.Id, task.Id, "file.bin", Guid.NewGuid().ToByteArray());

        var response = await admin.DeleteAsync(ApiRoutes.AdminUser(uploader.UserId));

        await AssertHandledAsync(response);
    }

    [Fact(Skip = KnownDefects.DeletingUserWithOwnedDataCausesServerError)]
    public async Task DeletingAUser_WhoOwnsAProjectGroup_IsHandledCleanly()
    {
        var admin = await _factory.AdminAsync();
        var owner = await _factory.CreateUserAsync("owner");
        await owner.CreateProjectGroupAsync();

        var response = await admin.DeleteAsync(ApiRoutes.AdminUser(owner.UserId));

        await AssertHandledAsync(response);
    }

    [Fact]
    public async Task Administrator_CannotDeleteTheirOwnAccount()
    {
        // Deleting the account you are signed in with leaves no way back in when it is the only administrator.
        var admin = await _factory.CreateAdminAsync();

        var response = await admin.DeleteAsync(ApiRoutes.AdminUser(admin.UserId));

        await AssertClientErrorAsync(response);
    }

    [Fact]
    public async Task Administrator_CannotLockTheirOwnAccount()
    {
        var admin = await _factory.CreateAdminAsync();

        var response = await admin.PutAsync(ApiRoutes.AdminUserLockout(admin.UserId), new SetUserLockoutRequest { LockedOut = true });

        await AssertClientErrorAsync(response);
    }

    [Fact]
    public async Task Administrator_CannotRemoveTheirOwnAdminRole()
    {
        var admin = await _factory.CreateAdminAsync();

        var response = await admin.PutAsync(ApiRoutes.AdminUserRoles(admin.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.User } });

        await AssertClientErrorAsync(response);
    }

    [Fact]
    public async Task LastAdministrator_CannotBeRemoved()
    {
        // With no administrator left nobody can manage users or settings any more.
        using var factory = new TaskHubApiFactory();
        var admin = await factory.AdminAsync();

        var delete = await admin.DeleteAsync(ApiRoutes.AdminUser(admin.UserId));
        var demote = await admin.PutAsync(ApiRoutes.AdminUserRoles(admin.UserId), new UpdateUserRolesRequest { Roles = new[] { Roles.User } });

        await AssertClientErrorAsync(delete);
        await AssertClientErrorAsync(demote);

        var relogin = await factory.LoginAsync(TaskHubApiFactory.AdminUserName, TaskHubApiFactory.AdminPassword);
        var adminPanel = await relogin.GetAsync(ApiRoutes.AdminUsers);
        await adminPanel.ShouldBeAsync(HttpStatusCode.OK);
    }

    private static async Task AssertClientErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var request = response.RequestMessage;

        Assert.True((int)response.StatusCode is >= 400 and < 500, $"{request?.Method} {request?.RequestUri?.PathAndQuery} returned {(int)response.StatusCode}, expected a 4xx refusal. Body: {body}");
    }

    private static async Task AssertHandledAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var request = response.RequestMessage;

        Assert.True((int)response.StatusCode < 500, $"{request?.Method} {request?.RequestUri?.PathAndQuery} ended in an unhandled error ({(int)response.StatusCode}). Body: {body}");
    }
}
