using System.Net;
using System.Security.Claims;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Auth;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Two-factor authentication: the password alone must not be enough, and the second step cannot be skipped or faked.
/// </summary>
public class TwoFactorSecurityTests : IClassFixture<TaskHubApiFactory>
{
    private readonly TaskHubApiFactory _factory;

    public TwoFactorSecurityTests(TaskHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Setup_ReturnsAKeyForTheAuthenticatorApp()
    {
        var user = await _factory.CreateUserAsync();

        var setup = await user.PostAsync<TwoFactorSetupResponse>(ApiRoutes.TwoFactorSetup);

        Assert.NotEmpty(setup.SharedKey);
        Assert.StartsWith("otpauth://totp/", setup.AuthenticatorUri);
        Assert.Contains(setup.SharedKey, setup.AuthenticatorUri);
    }

    [Fact]
    public async Task Enable_RequiresACodeFromTheAuthenticator()
    {
        var user = await _factory.CreateUserAsync();
        var setup = await user.PostAsync<TwoFactorSetupResponse>(ApiRoutes.TwoFactorSetup);

        var wrong = await user.PostAsync(ApiRoutes.TwoFactorEnable, new TwoFactorEnableRequest { Code = Authenticator.WrongCode(setup.SharedKey) });
        await wrong.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var me = await user.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.False(me.TwoFactorEnabled);

        var right = await user.PostAsync(ApiRoutes.TwoFactorEnable, new TwoFactorEnableRequest { Code = Authenticator.CurrentCode(setup.SharedKey) });
        await right.ShouldBeAsync(HttpStatusCode.NoContent);

        me = await user.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.True(me.TwoFactorEnabled);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    [InlineData("abcdef")]
    [InlineData("1")]
    [InlineData("' OR 1=1")]
    public async Task Enable_WithoutASetup_IsRefused(string code)
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.PostAsync(ApiRoutes.TwoFactorEnable, new TwoFactorEnableRequest { Code = code });

        await response.ShouldBeAsync(HttpStatusCode.BadRequest);

        var me = await user.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.False(me.TwoFactorEnabled);
    }

    [Fact]
    public async Task Login_WithTwoFactorEnabled_DoesNotIssueAnAccessTokenForThePasswordAlone()
    {
        var (user, _) = await CreateUserWithTwoFactorAsync();

        var login = await _factory.Anonymous().PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        Assert.True(login.RequiresTwoFactor);
        Assert.NotNull(login.TwoFactorToken);
        Assert.Null(login.AccessToken);
        Assert.Null(login.User);
    }

    [Fact]
    public async Task TwoFactorToken_DoesNotOpenTheApi()
    {
        var (user, _) = await CreateUserWithTwoFactorAsync();
        var login = await _factory.Anonymous().PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        var halfSignedIn = _factory.WithToken(login.TwoFactorToken!);

        var me = await halfSignedIn.GetAsync(ApiRoutes.Me);
        var projects = await halfSignedIn.GetAsync(ApiRoutes.Projects);
        var disable = await halfSignedIn.PostAsync(ApiRoutes.TwoFactorDisable, new TwoFactorDisableRequest { Password = user.Password });
        var changePassword = await halfSignedIn.PostAsync(ApiRoutes.Password, new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = "Another-Passw0rd-1" });

        await me.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await projects.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await disable.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await changePassword.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verify_WithTheRightCode_SignsTheUserIn()
    {
        var (user, sharedKey) = await CreateUserWithTwoFactorAsync();
        var anonymous = _factory.Anonymous();
        var login = await anonymous.PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        var verified = await anonymous.PostAsync<LoginResponse>(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest
        {
            TwoFactorToken = login.TwoFactorToken!,
            Code = Authenticator.CurrentCode(sharedKey)
        });

        Assert.False(verified.RequiresTwoFactor);
        Assert.Equal(user.UserId, verified.User!.Id);

        var me = await _factory.WithToken(verified.AccessToken!).GetAsync(ApiRoutes.Me);
        await me.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Verify_WithAWrongCode_IsRefused()
    {
        var (user, sharedKey) = await CreateUserWithTwoFactorAsync();
        var anonymous = _factory.Anonymous();
        var login = await anonymous.PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        var response = await anonymous.PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest
        {
            TwoFactorToken = login.TwoFactorToken!,
            Code = Authenticator.WrongCode(sharedKey)
        });

        await response.ReadErrorsAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verify_WithTheCodeOfAnotherUser_IsRefused()
    {
        var (user, _) = await CreateUserWithTwoFactorAsync();
        var (_, otherSharedKey) = await CreateUserWithTwoFactorAsync();
        var anonymous = _factory.Anonymous();
        var login = await anonymous.PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        var response = await anonymous.PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest
        {
            TwoFactorToken = login.TwoFactorToken!,
            Code = Authenticator.CurrentCode(otherSharedKey)
        });

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verify_WithAnAccessTokenInsteadOfTheTwoFactorToken_IsRefused()
    {
        // Otherwise any signed-in user could mint tokens for an account protected by 2FA knowing only its code.
        var (user, sharedKey) = await CreateUserWithTwoFactorAsync();
        var bystander = await _factory.CreateUserAsync();

        var withOwnAccessToken = await _factory.Anonymous().PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest
        {
            TwoFactorToken = user.Token!,
            Code = Authenticator.CurrentCode(sharedKey)
        });

        var withForeignAccessToken = await _factory.Anonymous().PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest
        {
            TwoFactorToken = bystander.Token!,
            Code = Authenticator.CurrentCode(sharedKey)
        });

        await withOwnAccessToken.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await withForeignAccessToken.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJwdXJwb3NlIjoiMmZhIn0.")]
    public async Task Verify_WithAMadeUpToken_IsRefused(string twoFactorToken)
    {
        var response = await _factory.Anonymous().PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest { TwoFactorToken = twoFactorToken, Code = "123456" });

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verify_WithATwoFactorTokenSignedByAnotherKey_IsRefused()
    {
        var (user, sharedKey) = await CreateUserWithTwoFactorAsync();
        var forge = new TokenForge(_factory.Jwt);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(AppClaimTypes.Purpose, "2fa")
        };
        var forged = forge.Create(claims, signingKey: "another-signing-key-that-the-server-does-not-know-0123456789");

        var response = await _factory.Anonymous().PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest { TwoFactorToken = forged, Code = Authenticator.CurrentCode(sharedKey) });

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RepeatedWrongCodes_LockTheAccount()
    {
        var (user, sharedKey) = await CreateUserWithTwoFactorAsync();
        var anonymous = _factory.Anonymous();
        var login = await anonymous.PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        for (var attempt = 0; attempt < TaskHubApiFactory.MaxFailedAccessAttempts; attempt++)
        {
            var wrong = await anonymous.PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest { TwoFactorToken = login.TwoFactorToken!, Code = Authenticator.WrongCode(sharedKey) });
            await wrong.ShouldBeAsync(HttpStatusCode.Unauthorized);
        }

        // Guessing the six digits is cut short: even the right code no longer works.
        var right = await anonymous.PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest { TwoFactorToken = login.TwoFactorToken!, Code = Authenticator.CurrentCode(sharedKey) });
        var passwordLogin = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        await right.ShouldBeAsync(HttpStatusCode.Unauthorized);
        await passwordLogin.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_WhileTwoFactorIsEnabled_IsRefused()
    {
        // A stolen session must not be able to replace the authenticator key.
        var (user, sharedKey) = await CreateUserWithTwoFactorAsync();

        var response = await user.PostAsync(ApiRoutes.TwoFactorSetup);

        await response.ReadErrorsAsync(HttpStatusCode.Conflict);

        // The original key still works.
        var anonymous = _factory.Anonymous();
        var login = await anonymous.PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        var verified = await anonymous.PostAsync(ApiRoutes.TwoFactorVerify, new TwoFactorVerifyRequest { TwoFactorToken = login.TwoFactorToken!, Code = Authenticator.CurrentCode(sharedKey) });

        await verified.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Disable_RequiresThePassword()
    {
        // A signed-in session alone must not be enough to remove the second factor.
        var (user, _) = await CreateUserWithTwoFactorAsync();

        var wrong = await user.PostAsync(ApiRoutes.TwoFactorDisable, new TwoFactorDisableRequest { Password = "Wrong-Passw0rd-1" });
        var missing = await user.PostAsync(ApiRoutes.TwoFactorDisable, new { });

        await wrong.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await missing.ShouldBeAsync(HttpStatusCode.BadRequest);

        var me = await user.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.True(me.TwoFactorEnabled);
    }

    [Fact]
    public async Task Disable_WithThePassword_RestoresThePasswordOnlyLogin()
    {
        var (user, _) = await CreateUserWithTwoFactorAsync();

        var disable = await user.PostAsync(ApiRoutes.TwoFactorDisable, new TwoFactorDisableRequest { Password = user.Password });
        await disable.ShouldBeAsync(HttpStatusCode.NoContent);

        var login = await _factory.Anonymous().PostAsync<LoginResponse>(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });

        Assert.False(login.RequiresTwoFactor);
        Assert.NotNull(login.AccessToken);
    }

    [Fact]
    public async Task TwoFactorOfOneUser_CannotBeChangedByAnother()
    {
        // The endpoints act on the caller only; a user id in the body must be ignored.
        var (victim, _) = await CreateUserWithTwoFactorAsync();
        var attacker = await _factory.CreateUserAsync("attacker");

        var response = await attacker.PostAsync(ApiRoutes.TwoFactorDisable, new { password = attacker.Password, userId = victim.UserId, userName = victim.UserName });
        await response.ShouldBeAsync(HttpStatusCode.NoContent);

        var me = await victim.GetAsync<UserDto>(ApiRoutes.Me);
        Assert.True(me.TwoFactorEnabled);
    }

    private async Task<(ApiSession User, string SharedKey)> CreateUserWithTwoFactorAsync()
    {
        var user = await _factory.CreateUserAsync("twofactor");
        var setup = await user.PostAsync<TwoFactorSetupResponse>(ApiRoutes.TwoFactorSetup);

        var enable = await user.PostAsync(ApiRoutes.TwoFactorEnable, new TwoFactorEnableRequest { Code = Authenticator.CurrentCode(setup.SharedKey) });
        await enable.ShouldBeAsync(HttpStatusCode.NoContent);

        return (user, setup.SharedKey);
    }
}
