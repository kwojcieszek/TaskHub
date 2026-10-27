using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;
using TaskHub.Tests.Support;

namespace TaskHub.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private const int MaxFailedAttempts = 3;

    private readonly TestHost _host = new(ConfigureSecurity);
    private readonly IAuthService _auth;

    public AuthServiceTests()
    {
        _auth = _host.Get<IAuthService>();
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task Register_IsRefusedWhileRegistrationIsClosed()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _auth.RegisterAsync(NewRegistration()));
        Assert.Null(await _host.Users.FindByNameAsync("newcomer"));
    }

    [Fact]
    public async Task Register_CreatesAnOrdinaryUserOnceRegistrationIsOpened()
    {
        await OpenRegistrationAsync();

        var user = await _auth.RegisterAsync(NewRegistration());

        Assert.Equal(new[] { Roles.User }, user.Roles);
        Assert.False(user.MustChangePassword);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsATokenAndLogsTheSignIn()
    {
        var user = await _host.CreateUserAsync("anna", Roles.User);

        var response = await LoginAsync("anna", TestHost.DefaultPassword);
        var log = await _host.Db.UserActivityLogs.AsNoTracking().SingleAsync(entry => entry.Type == UserActivityType.Login);

        Assert.False(response.RequiresTwoFactor);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.Equal(user.Id, response.User!.Id);
        Assert.Null(log.FailureReason);
        Assert.Equal(user.Id, log.UserId);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownUser_IsRejectedAndLogged()
    {
        await _host.CreateUserAsync("anna");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => LoginAsync("anna", "wrong"));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => LoginAsync("nobody", "wrong"));
        var reasons = await _host.Db.UserActivityLogs.AsNoTracking().Where(log => log.Type == UserActivityType.Login).Select(log => log.FailureReason).ToListAsync();

        Assert.Contains(LoginFailureReason.InvalidPassword, reasons);
        Assert.Contains(LoginFailureReason.UserNotFound, reasons);
    }

    [Fact]
    public async Task Login_LocksTheAccountAfterTheConfiguredNumberOfFailures()
    {
        await _host.CreateUserAsync("anna");

        for (var attempt = 0; attempt < MaxFailedAttempts; attempt++)
        {
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => LoginAsync("anna", "wrong"));
        }

        // Even the right password is refused while the lockout lasts.
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => LoginAsync("anna", TestHost.DefaultPassword));
        Assert.True(await _host.Users.IsLockedOutAsync((await _host.Users.FindByNameAsync("anna"))!));
    }

    [Fact]
    public async Task Login_ResetsTheFailureCounterAfterASuccess()
    {
        var user = await _host.CreateUserAsync("anna");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => LoginAsync("anna", "wrong"));
        await LoginAsync("anna", TestHost.DefaultPassword);

        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task ChangePassword_ReplacesThePasswordAndEndsOtherSessions()
    {
        var user = await _host.CreateUserAsync("anna");
        user.MustChangePassword = true;
        await _host.Users.UpdateAsync(user);

        var response = await _auth.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = TestHost.DefaultPassword,
            NewPassword = "NewPassword2"
        });

        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.False(response.User!.MustChangePassword);
        Assert.Equal(1, user.TokenVersion);
        Assert.True(await _host.Users.CheckPasswordAsync(user, "NewPassword2"));
        Assert.False(await _host.Users.CheckPasswordAsync(user, TestHost.DefaultPassword));
    }

    [Fact]
    public async Task ChangePassword_IsRefusedWithAWrongCurrentPasswordOrAnUnchangedOne()
    {
        var user = await _host.CreateUserAsync("anna");

        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "wrong",
            NewPassword = "NewPassword2"
        }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = TestHost.DefaultPassword,
            NewPassword = TestHost.DefaultPassword
        }));

        Assert.Equal(0, user.TokenVersion);
    }

    [Fact]
    public async Task TwoFactor_LoginAsksForTheCodeAndCompletesWithIt()
    {
        var user = await _host.CreateUserAsync("anna");
        var key = await EnableTwoFactorAsync(user);

        var firstStep = await LoginAsync("anna", TestHost.DefaultPassword);
        var secondStep = await _auth.VerifyTwoFactorAsync(new TwoFactorVerifyRequest
        {
            TwoFactorToken = firstStep.TwoFactorToken!,
            Code = CodeFor(key)
        });

        Assert.True(firstStep.RequiresTwoFactor);
        Assert.Null(firstStep.AccessToken);
        Assert.False(string.IsNullOrEmpty(secondStep.AccessToken));
    }

    [Fact]
    public async Task TwoFactor_VerifyRejectsAWrongCodeAndAnAccessTokenUsedAsTheFirstStep()
    {
        var user = await _host.CreateUserAsync("anna");
        await EnableTwoFactorAsync(user);
        var firstStep = await LoginAsync("anna", TestHost.DefaultPassword);
        var (accessToken, _) = _host.Get<IJwtTokenService>().CreateAccessToken(user, Array.Empty<string>());

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _auth.VerifyTwoFactorAsync(new TwoFactorVerifyRequest
        {
            TwoFactorToken = firstStep.TwoFactorToken!,
            Code = "000000"
        }));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _auth.VerifyTwoFactorAsync(new TwoFactorVerifyRequest
        {
            TwoFactorToken = accessToken,
            Code = "000000"
        }));
    }

    [Fact]
    public async Task TwoFactor_SetupCannotBeRestartedWhileItIsEnabled()
    {
        var user = await _host.CreateUserAsync("anna");
        await EnableTwoFactorAsync(user);

        await Assert.ThrowsAsync<ConflictException>(() => _auth.BeginTwoFactorSetupAsync(user.Id));
    }

    [Fact]
    public async Task TwoFactor_DisableNeedsThePassword()
    {
        var user = await _host.CreateUserAsync("anna");
        await EnableTwoFactorAsync(user);

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _auth.DisableTwoFactorAsync(user.Id, new TwoFactorDisableRequest { Password = "wrong" }));
        var stillEnabled = user.TwoFactorEnabled;
        await _auth.DisableTwoFactorAsync(user.Id, new TwoFactorDisableRequest { Password = TestHost.DefaultPassword });

        Assert.True(stillEnabled);
        Assert.False(user.TwoFactorEnabled);
    }

    [Fact]
    public async Task Google_LoginWorksOnlyForAnAccountLinkedBeforehand()
    {
        var user = await _host.CreateUserAsync("anna");
        var google = new GoogleUserInfo { Subject = "google-1", Email = "anna@example.test", EmailVerified = true };
        _host.Get<FakeGoogleTokenValidator>().Register("valid-token", google);
        var request = new GoogleTokenRequest { IdToken = "valid-token", Password = TestHost.DefaultPassword };

        // The matching e-mail address alone does not sign anyone in.
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _auth.LoginWithGoogleAsync(request));

        var linked = await _auth.LinkGoogleAsync(user.Id, request);
        var response = await _auth.LoginWithGoogleAsync(request);

        Assert.True(linked.GoogleLinked);
        Assert.Equal(user.Id, response.User!.Id);
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _auth.LoginWithGoogleAsync(new GoogleTokenRequest { IdToken = "forged-token" }));
    }

    [Fact]
    public async Task Google_AccountCannotBeLinkedToTwoUsersOrWithoutAVerifiedEmail()
    {
        var first = await _host.CreateUserAsync("anna");
        var second = await _host.CreateUserAsync("jan");
        var validator = _host.Get<FakeGoogleTokenValidator>();
        validator.Register("verified", new GoogleUserInfo { Subject = "google-1", Email = "a@example.test", EmailVerified = true });
        validator.Register("unverified", new GoogleUserInfo { Subject = "google-2", Email = "b@example.test", EmailVerified = false });
        await _auth.LinkGoogleAsync(first.Id, new GoogleTokenRequest { IdToken = "verified", Password = TestHost.DefaultPassword });

        await Assert.ThrowsAsync<ConflictException>(() => _auth.LinkGoogleAsync(second.Id, new GoogleTokenRequest { IdToken = "verified", Password = TestHost.DefaultPassword }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.LinkGoogleAsync(second.Id, new GoogleTokenRequest { IdToken = "unverified", Password = TestHost.DefaultPassword }));
    }

    [Fact]
    public async Task TwoFactor_ChallengeIsSingleUse()
    {
        var user = await _host.CreateUserAsync("singleuse");
        var key = await EnableTwoFactorAsync(user);
        var login = await LoginAsync(user.UserName!, TestHost.DefaultPassword);
        var request = new TwoFactorVerifyRequest { TwoFactorToken = login.TwoFactorToken!, Code = CodeFor(key) };
        await _auth.VerifyTwoFactorAsync(request);
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _auth.VerifyTwoFactorAsync(request));
        Assert.Single(await _host.Db.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task TwoFactor_ChallengeIsInvalidAfterPasswordChange()
    {
        var user = await _host.CreateUserAsync("revokedchallenge");
        var key = await EnableTwoFactorAsync(user);
        var login = await LoginAsync(user.UserName!, TestHost.DefaultPassword);
        await _auth.ChangePasswordAsync(user.Id, new ChangePasswordRequest { CurrentPassword = TestHost.DefaultPassword, NewPassword = "NewPassword2" });
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _auth.VerifyTwoFactorAsync(new TwoFactorVerifyRequest { TwoFactorToken = login.TwoFactorToken!, Code = CodeFor(key) }));
    }

    [Fact]
    public async Task GoogleLink_RequiresFreshPassword()
    {
        var user = await _host.CreateUserAsync("freshpassword");
        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.LinkGoogleAsync(user.Id, new GoogleTokenRequest { IdToken = "anything", Password = "wrong" }));
        Assert.Empty(await _host.Users.GetLoginsAsync(user));
    }

    [Fact]
    public async Task PasswordReset_MailsALinkThatSetsANewPasswordAndEndsSessions()
    {
        var user = await _host.CreateUserAsync("anna");
        user.MustChangePassword = true;
        await _host.Users.UpdateAsync(user);

        await _auth.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = user.Email! });
        var message = Assert.Single(_host.Email.Sent);
        await _auth.ResetPasswordAsync(new ResetPasswordRequest { Email = user.Email!, Token = ResetTokenFrom(message.TextBody), NewPassword = "NewPassword2" });

        Assert.Equal(user.Email, message.To);
        Assert.StartsWith(TestHost.ResetUrl, ResetLinkFrom(message.TextBody));
        Assert.True(await _host.Users.CheckPasswordAsync(user, "NewPassword2"));
        Assert.False(user.MustChangePassword);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task PasswordReset_ForAnUnknownAddress_SendsNothingAndDoesNotFail()
    {
        await _auth.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = "nobody@example.test" });

        Assert.Empty(_host.Email.Sent);
    }

    [Fact]
    public async Task PasswordReset_LinkWorksOnlyOnce()
    {
        var user = await _host.CreateUserAsync("anna");
        await _auth.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = user.Email! });
        var token = ResetTokenFrom(_host.Email.Sent.Single().TextBody);

        await _auth.ResetPasswordAsync(new ResetPasswordRequest { Email = user.Email!, Token = token, NewPassword = "NewPassword2" });

        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.ResetPasswordAsync(new ResetPasswordRequest { Email = user.Email!, Token = token, NewPassword = "NewPassword3" }));
        Assert.True(await _host.Users.CheckPasswordAsync(user, "NewPassword2"));
    }

    [Fact]
    public async Task PasswordReset_WithAForeignOrForgedToken_IsRefused()
    {
        var anna = await _host.CreateUserAsync("anna");
        var bob = await _host.CreateUserAsync("bob");
        await _auth.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = anna.Email! });
        var annasToken = ResetTokenFrom(_host.Email.Sent.Single().TextBody);

        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.ResetPasswordAsync(new ResetPasswordRequest { Email = bob.Email!, Token = annasToken, NewPassword = "NewPassword2" }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.ResetPasswordAsync(new ResetPasswordRequest { Email = anna.Email!, Token = "not-a-token!", NewPassword = "NewPassword2" }));

        Assert.True(await _host.Users.CheckPasswordAsync(bob, TestHost.DefaultPassword));
        Assert.True(await _host.Users.CheckPasswordAsync(anna, TestHost.DefaultPassword));
    }

    [Fact]
    public async Task PasswordReset_IsUnavailableWithoutAMailServer()
    {
        var user = await _host.CreateUserAsync("anna");
        _host.Email.IsConfigured = false;

        var providers = await _auth.GetProvidersAsync();

        Assert.False(providers.PasswordResetEnabled);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _auth.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = user.Email! }));
        Assert.Empty(_host.Email.Sent);
    }

    private static void ConfigureSecurity(SecurityOptions options)
    {
        options.LockoutEnabled = true;
        options.MaxFailedAccessAttempts = MaxFailedAttempts;
        options.LockoutMinutes = 15;
        options.RegistrationEnabled = false;
    }

    private static RegisterRequest NewRegistration()
    {
        return new RegisterRequest
        {
            UserName = "newcomer",
            Email = "newcomer@example.test",
            Password = TestHost.DefaultPassword,
            DisplayName = "Newcomer"
        };
    }

    private async Task OpenRegistrationAsync()
    {
        var settings = _host.Get<ISecuritySettingsService>();
        var current = await settings.GetAsync();
        current.RegistrationEnabled = true;

        await settings.UpdateAsync(current);
    }

    private Task<LoginResponse> LoginAsync(string userName, string password)
    {
        return _auth.LoginAsync(new LoginRequest { UserName = userName, Password = password });
    }

    private async Task<string> EnableTwoFactorAsync(User user)
    {
        var setup = await _auth.BeginTwoFactorSetupAsync(user.Id);
        await _auth.EnableTwoFactorAsync(user.Id, CodeFor(setup.SharedKey));

        return setup.SharedKey;
    }

    private static string CodeFor(string sharedKey)
    {
        return new Totp(Base32Encoding.ToBytes(sharedKey)).ComputeTotp();
    }

    private static string ResetLinkFrom(string body)
    {
        return body.Split('\n').Single(line => line.StartsWith(TestHost.ResetUrl));
    }

    private static string ResetTokenFrom(string body)
    {
        var query = QueryHelpers.ParseQuery(new Uri(ResetLinkFrom(body)).Query);

        return query["token"].ToString();
    }
}
