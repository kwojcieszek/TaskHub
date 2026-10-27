using System.Net;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

public class SessionAndHistoryTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;
    public SessionAndHistoryTests(TaskHubApiFactory factory) => _factory = factory;
    private const string Sessions = ApiRoutes.Root + "/auth/sessions";

    [Fact]
    public async Task SessionsArePrivateAndRevocationInvalidatesTheToken()
    {
        var first = await _factory.CreateUserAsync("sessions");
        var stranger = await _factory.CreateUserAsync("sessionstranger");
        var session = Assert.Single(await first.GetAsync<List<SessionDto>>(Sessions));
        Assert.True(session.IsCurrent);
        await (await stranger.DeleteAsync($"{Sessions}/{session.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await first.DeleteAsync($"{Sessions}/{session.Id}")).ShouldBeAsync(HttpStatusCode.NoContent);
        await (await first.GetAsync(ApiRoutes.Me)).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutAllEndsEverySessionOfOnlyThisUser()
    {
        var first = await _factory.CreateUserAsync("allsessions");
        var stranger = await _factory.CreateUserAsync("unrelatedsession");
        var login = await _factory.Anonymous().PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = first.UserName, Password = first.Password });
        var second = _factory.WithToken(login.AccessToken!);
        Assert.Equal(2, (await first.GetAsync<List<SessionDto>>(Sessions)).Count);
        await (await first.PostAsync(Sessions + "/logout?all=true")).ShouldBeAsync(HttpStatusCode.NoContent);
        await (await first.GetAsync(ApiRoutes.Me)).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await second.GetAsync(ApiRoutes.Me)).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await stranger.GetAsync(ApiRoutes.Me)).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HistoryTracksChangesAndRefusesOtherUsers()
    {
        var owner = await _factory.CreateUserAsync("historyowner");
        var stranger = await _factory.CreateUserAsync("historystranger");
        var project = await owner.CreateProjectAsync();
        var task = await owner.CreateTaskAsync(project.Id);
        var url = ApiRoutes.Task(project.Id, task.Id) + "/history";
        var update = new UpdateTaskItemRequest { Title = "Changed title", Priority = task.Priority, Status = task.Status };
        await owner.PutAsync<TaskItemDto>(ApiRoutes.Task(project.Id, task.Id), update);
        var history = await owner.GetAsync<List<TaskChangeDto>>(url);
        Assert.Contains(history, h => h.Field == "Created");
        Assert.Contains(history, h => h.Field == "Title" && h.OldValue == task.Title && h.NewValue == update.Title);
        await (await stranger.GetAsync(url)).ShouldBeAsync(HttpStatusCode.Forbidden);
        await owner.PutAsync<TaskItemDto>(ApiRoutes.Task(project.Id, task.Id), update);
        Assert.Equal(history.Count, (await owner.GetAsync<List<TaskChangeDto>>(url)).Count);
    }
}
