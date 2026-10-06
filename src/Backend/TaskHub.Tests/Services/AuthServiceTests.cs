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
        var log = await _host.Db.LoginLogs.AsNoTracking().SingleAsync();

        Assert.False(response.RequiresTwoFactor);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.Equal(user.Id, response.User!.Id);
        Assert.True(log.Succeeded);
        Assert.Equal(user.Id, log.UserId);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownUser_IsRejectedAndLogged()
    {
        await _host.CreateUserAsync("anna");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => LoginAsync("anna", "wrong"));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => LoginAsync("nobody", "wrong"));
        var reasons = await _host.Db.LoginLogs.AsNoTracking().Select(log => log.FailureReason).ToListAsync();

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
        var request = new GoogleTokenRequest { IdToken = "valid-token" };

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
        await _auth.LinkGoogleAsync(first.Id, new GoogleTokenRequest { IdToken = "verified" });

        await Assert.ThrowsAsync<ConflictException>(() => _auth.LinkGoogleAsync(second.Id, new GoogleTokenRequest { IdToken = "verified" }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => _auth.LinkGoogleAsync(second.Id, new GoogleTokenRequest { IdToken = "unverified" }));
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

    private async Task<string> EnableTwoFactorAsync(ApplicationUser user)
    {
        var setup = await _auth.BeginTwoFactorSetupAsync(user.Id);
        await _auth.EnableTwoFactorAsync(user.Id, CodeFor(setup.SharedKey));

        return setup.SharedKey;
    }

    private static string CodeFor(string sharedKey)
    {
        return new Totp(Base32Encoding.ToBytes(sharedKey)).ComputeTotp();
    }
}
