using System.Net;
using TaskHub.Application.Dtos.Auth;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// A forgotten password is replaced through a single-use link sent to the account's e-mail address,
/// without revealing to the caller which addresses belong to an account.
/// </summary>
public class PasswordResetTests : IClassFixture<TaskHubApiFactory>
{
    private const string NewPassword = "Forgotten-Passw0rd-2";

    private readonly TaskHubApiFactory _factory;

    public PasswordResetTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Providers_ReportThatPasswordResetIsAvailable()
    {
        var providers = await _factory.Anonymous().GetAsync<AuthProvidersDto>(ApiRoutes.Providers);

        Assert.True(providers.PasswordResetEnabled);
    }

    [Fact]
    public async Task ForgotPassword_AnswersTheSameForKnownAndUnknownAddresses()
    {
        const string unknown = "nobody-here@example.test";
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        var known = await anonymous.PostAsync(ApiRoutes.ForgotPassword, new ForgotPasswordRequest { Email = user.Email });
        var missing = await anonymous.PostAsync(ApiRoutes.ForgotPassword, new ForgotPasswordRequest { Email = unknown });

        await known.ShouldBeAsync(HttpStatusCode.NoContent);
        await missing.ShouldBeAsync(HttpStatusCode.NoContent);
        Assert.Single(_factory.Email.SentTo(user.Email));
        Assert.Empty(_factory.Email.SentTo(unknown));
    }

    [Fact]
    public async Task ResetPassword_ThroughTheMailedLink_ReplacesThePasswordAndEndsSessions()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        await RequestResetAsync(anonymous, user.Email);
        var link = _factory.Email.LastLinkQueryFor(user.Email);
        var reset = await anonymous.PostAsync(ApiRoutes.ResetPassword, new ResetPasswordRequest { Email = link["email"], Token = link["token"], NewPassword = NewPassword });

        await reset.ShouldBeAsync(HttpStatusCode.NoContent);
        await (await user.GetAsync(ApiRoutes.Me)).ShouldBeAsync(HttpStatusCode.Unauthorized);

        var signedIn = await _factory.LoginAsync(user.UserName, NewPassword);
        await (await signedIn.GetAsync(ApiRoutes.Projects)).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_LinkCannotBeUsedTwice()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        await RequestResetAsync(anonymous, user.Email);
        var link = _factory.Email.LastLinkQueryFor(user.Email);
        var first = await anonymous.PostAsync(ApiRoutes.ResetPassword, new ResetPasswordRequest { Email = link["email"], Token = link["token"], NewPassword = NewPassword });
        await first.ShouldBeAsync(HttpStatusCode.NoContent);

        var replay = await anonymous.PostAsync(ApiRoutes.ResetPassword, new ResetPasswordRequest { Email = link["email"], Token = link["token"], NewPassword = "Replayed-Passw0rd-3" });

        var errors = await replay.ReadErrorsAsync(HttpStatusCode.BadRequest);
        Assert.Contains("invalid or has expired", errors[0]);
    }

    [Fact]
    public async Task ResetPassword_WithAnotherUsersToken_IsRefused()
    {
        var victim = await _factory.CreateUserAsync("victim");
        var attacker = await _factory.CreateUserAsync("attacker");

        await RequestResetAsync(attacker, attacker.Email);
        var attackersLink = _factory.Email.LastLinkQueryFor(attacker.Email);

        var response = await attacker.PostAsync(ApiRoutes.ResetPassword, new ResetPasswordRequest { Email = victim.Email, Token = attackersLink["token"], NewPassword = NewPassword });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await victim.GetAsync(ApiRoutes.Me)).ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_StillEnforcesThePasswordPolicy()
    {
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        await RequestResetAsync(anonymous, user.Email);
        var link = _factory.Email.LastLinkQueryFor(user.Email);

        var response = await anonymous.PostAsync(ApiRoutes.ResetPassword, new ResetPasswordRequest { Email = link["email"], Token = link["token"], NewPassword = "weak" });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await user.GetAsync(ApiRoutes.Me)).ShouldBeAsync(HttpStatusCode.OK);
    }

    private static async Task RequestResetAsync(ApiSession session, string email)
    {
        var response = await session.PostAsync(ApiRoutes.ForgotPassword, new ForgotPasswordRequest { Email = email });

        await response.ShouldBeAsync(HttpStatusCode.NoContent);
    }
}
