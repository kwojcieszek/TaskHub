using System.Net;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Admin;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Security;
using TaskHub.IntegrationTests.Support;

namespace TaskHub.IntegrationTests.Security;

/// <summary>
/// Self-registration and the security settings behind it. Every test changes global settings,
/// so each one runs against an API instance of its own.
/// </summary>
public class RegistrationSecurityTests : IDisposable
{
    private readonly TaskHubApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task Registration_IsClosedByDefault()
    {
        var response = await _factory.Anonymous().PostAsync(ApiRoutes.Register, NewRegistration());

        await response.ReadErrorsAsync(HttpStatusCode.Forbidden);

        var providers = await _factory.Anonymous().GetAsync<AuthProvidersDto>(ApiRoutes.Providers);
        Assert.False(providers.RegistrationEnabled);
    }

    [Fact]
    public async Task Registration_WhenOpened_CreatesAnOrdinaryUser()
    {
        await UpdateSecurityAsync(settings => settings.RegistrationEnabled = true);
        var registration = NewRegistration();

        var created = await _factory.Anonymous().PostAsync<UserDto>(ApiRoutes.Register, registration, HttpStatusCode.Created);

        Assert.Equal(new[] { Roles.User }, created.Roles);
        Assert.False(created.MustChangePassword);

        var user = await _factory.LoginAsync(registration.UserName, registration.Password);
        var projects = await user.GetAsync(ApiRoutes.Projects);
        var adminPanel = await user.GetAsync(ApiRoutes.AdminUsers);

        await projects.ShouldBeAsync(HttpStatusCode.OK);
        await adminPanel.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Registration_IgnoresPrivilegesSentWithTheRequest()
    {
        await UpdateSecurityAsync(settings => settings.RegistrationEnabled = true);
        var userName = NewUserName();

        var response = await _factory.Anonymous().PostAsync(ApiRoutes.Register, new
        {
            userName,
            email = $"{userName}@example.test",
            password = TaskHubApiFactory.UserPassword,
            roles = new[] { Roles.Admin },
            role = Roles.Admin,
            isAdmin = true,
            id = Guid.Empty,
            tokenVersion = 99,
            mustChangePassword = false,
            lockoutEnabled = false,
            emailConfirmed = true,
            twoFactorEnabled = false
        });

        var created = await response.ReadAsync<UserDto>(HttpStatusCode.Created);
        Assert.Equal(new[] { Roles.User }, created.Roles);
        Assert.NotEqual(Guid.Empty, created.Id);

        var user = await _factory.LoginAsync(userName, TaskHubApiFactory.UserPassword);
        var adminPanel = await user.GetAsync(ApiRoutes.AdminUsers);
        await adminPanel.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Registration_CannotTakeAnExistingUserNameOrEmail()
    {
        await UpdateSecurityAsync(settings => settings.RegistrationEnabled = true);
        var first = NewRegistration();
        await _factory.Anonymous().PostAsync<UserDto>(ApiRoutes.Register, first, HttpStatusCode.Created);

        var sameName = NewRegistration();
        sameName.UserName = first.UserName;

        var sameNameOtherCase = NewRegistration();
        sameNameOtherCase.UserName = first.UserName.ToUpperInvariant();

        var sameEmail = NewRegistration();
        sameEmail.Email = first.Email;

        var adminName = NewRegistration();
        adminName.UserName = TaskHubApiFactory.AdminUserName;

        foreach (var request in new[] { sameName, sameNameOtherCase, sameEmail, adminName })
        {
            var response = await _factory.Anonymous().PostAsync(ApiRoutes.Register, request);
            await response.ReadErrorsAsync(HttpStatusCode.BadRequest);
        }

        // The administrator account is untouched by the attempt to register its name.
        var admin = await _factory.AdminAsync();
        var users = await admin.GetAsync<List<AdminUserDto>>(ApiRoutes.AdminUsers);
        Assert.Single(users, user => string.Equals(user.UserName, TaskHubApiFactory.AdminUserName, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ab", "valid@example.test", "User-Passw0rd-1")]
    [InlineData("", "valid@example.test", "User-Passw0rd-1")]
    [InlineData("validname", "not-an-email", "User-Passw0rd-1")]
    [InlineData("validname", "", "User-Passw0rd-1")]
    [InlineData("validname", "valid@example.test", "")]
    [InlineData("validname", "valid@example.test", "short1A")]
    [InlineData("validname", "valid@example.test", "nouppercase1")]
    [InlineData("validname", "valid@example.test", "NOLOWERCASE1")]
    [InlineData("validname", "valid@example.test", "NoDigitsAtAll")]
    [InlineData("name with spaces", "valid@example.test", "User-Passw0rd-1")]
    [InlineData("<script>alert(1)</script>", "valid@example.test", "User-Passw0rd-1")]
    [InlineData("name'; DROP TABLE AspNetUsers;--", "valid@example.test", "User-Passw0rd-1")]
    public async Task Registration_RejectsInvalidData(string userName, string email, string password)
    {
        await UpdateSecurityAsync(settings => settings.RegistrationEnabled = true);

        var response = await _factory.Anonymous().PostAsync(ApiRoutes.Register, new RegisterRequest { UserName = userName, Email = email, Password = password });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var admin = await _factory.AdminAsync();
        var users = await admin.GetAsync<List<AdminUserDto>>(ApiRoutes.AdminUsers);
        Assert.Single(users);
    }

    [Fact]
    public async Task Registration_ClosedAgain_StopsNewAccountsButKeepsExistingOnes()
    {
        await UpdateSecurityAsync(settings => settings.RegistrationEnabled = true);
        var registration = NewRegistration();
        await _factory.Anonymous().PostAsync<UserDto>(ApiRoutes.Register, registration, HttpStatusCode.Created);

        await UpdateSecurityAsync(settings => settings.RegistrationEnabled = false);

        var refused = await _factory.Anonymous().PostAsync(ApiRoutes.Register, NewRegistration());
        await refused.ShouldBeAsync(HttpStatusCode.Forbidden);

        var existing = await _factory.LoginAsync(registration.UserName, registration.Password);
        Assert.NotNull(existing.Token);
    }

    [Fact]
    public async Task PasswordPolicy_ChangedByTheAdministrator_AppliesToNewPasswords()
    {
        await UpdateSecurityAsync(settings =>
        {
            settings.RegistrationEnabled = true;
            settings.PasswordPolicy.MinLength = 12;
            settings.PasswordPolicy.RequireNonAlphanumeric = true;
        });

        var providers = await _factory.Anonymous().GetAsync<AuthProvidersDto>(ApiRoutes.Providers);
        Assert.Equal(12, providers.PasswordPolicy.MinLength);
        Assert.True(providers.PasswordPolicy.RequireNonAlphanumeric);

        var tooShort = NewRegistration();
        tooShort.Password = "Sh0rt-Pass";

        var noSymbol = NewRegistration();
        noSymbol.Password = "LongEnoughPassw0rd";

        var valid = NewRegistration();
        valid.Password = "Long-Enough-Passw0rd";

        var tooShortResponse = await _factory.Anonymous().PostAsync(ApiRoutes.Register, tooShort);
        var noSymbolResponse = await _factory.Anonymous().PostAsync(ApiRoutes.Register, noSymbol);
        var validResponse = await _factory.Anonymous().PostAsync(ApiRoutes.Register, valid);

        await tooShortResponse.ShouldBeAsync(HttpStatusCode.BadRequest);
        await noSymbolResponse.ShouldBeAsync(HttpStatusCode.BadRequest);
        await validResponse.ShouldBeAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PasswordPolicy_AppliesToPasswordsAssignedByAnAdministrator()
    {
        await UpdateSecurityAsync(settings => settings.PasswordPolicy.MinLength = 20);
        var admin = await _factory.AdminAsync();
        var userName = NewUserName();

        var create = await admin.PostAsync(ApiRoutes.AdminUsers, new CreateUserRequest { UserName = userName, Email = $"{userName}@example.test", Password = TaskHubApiFactory.AssignedPassword });
        var reset = await admin.PutAsync(ApiRoutes.AdminUserPassword(admin.UserId), new ResetUserPasswordRequest { NewPassword = "Short-Passw0rd-1" });

        await create.ReadErrorsAsync(HttpStatusCode.BadRequest);
        await reset.ReadErrorsAsync(HttpStatusCode.BadRequest);

        // The refused reset did not end the administrator's session.
        var stillSignedIn = await admin.GetAsync(ApiRoutes.AdminUsers);
        await stillSignedIn.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(0, 15, 8)]
    [InlineData(-1, 15, 8)]
    [InlineData(101, 15, 8)]
    [InlineData(5, 0, 8)]
    [InlineData(5, -10, 8)]
    [InlineData(5, 525601, 8)]
    [InlineData(5, 15, 3)]
    [InlineData(5, 15, 0)]
    [InlineData(5, 15, -1)]
    [InlineData(5, 15, 65)]
    public async Task SecuritySettings_OutOfRange_AreRejected(int maxFailedAccessAttempts, int lockoutMinutes, int minPasswordLength)
    {
        var admin = await _factory.AdminAsync();
        var before = await admin.GetAsync<SecuritySettingsDto>(ApiRoutes.AdminSecurity);

        var response = await admin.PutAsync(ApiRoutes.AdminSecurity, new SecuritySettingsDto
        {
            LockoutEnabled = true,
            MaxFailedAccessAttempts = maxFailedAccessAttempts,
            LockoutMinutes = lockoutMinutes,
            RegistrationEnabled = false,
            PasswordPolicy = new PasswordPolicyDto { MinLength = minPasswordLength }
        });

        await response.ReadErrorsAsync(HttpStatusCode.BadRequest);

        var after = await admin.GetAsync<SecuritySettingsDto>(ApiRoutes.AdminSecurity);
        Assert.Equal(before.MaxFailedAccessAttempts, after.MaxFailedAccessAttempts);
        Assert.Equal(before.LockoutMinutes, after.LockoutMinutes);
        Assert.Equal(before.PasswordPolicy.MinLength, after.PasswordPolicy.MinLength);
    }

    [Fact]
    public async Task Lockout_SwitchedOff_NoLongerLocksAccounts()
    {
        await UpdateSecurityAsync(settings => settings.LockoutEnabled = false);
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        for (var attempt = 0; attempt < TaskHubApiFactory.MaxFailedAccessAttempts * 3; attempt++)
        {
            var failed = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });
            await failed.ShouldBeAsync(HttpStatusCode.Unauthorized);
        }

        var login = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        await login.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lockout_ThresholdChangedByTheAdministrator_IsApplied()
    {
        await UpdateSecurityAsync(settings => settings.MaxFailedAccessAttempts = 1);
        var user = await _factory.CreateUserAsync();
        var anonymous = _factory.Anonymous();

        await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = "Wrong-Passw0rd-1" });

        var login = await anonymous.PostAsync(ApiRoutes.Login, new LoginRequest { UserName = user.UserName, Password = user.Password });
        await login.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    private static string NewUserName()
    {
        return $"reg{Guid.NewGuid():N}"[..20];
    }

    private static RegisterRequest NewRegistration()
    {
        var userName = NewUserName();

        return new RegisterRequest
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            Password = TaskHubApiFactory.UserPassword,
            DisplayName = "Registered user"
        };
    }

    private async Task UpdateSecurityAsync(Action<SecuritySettingsDto> change)
    {
        var admin = await _factory.AdminAsync();
        var settings = await admin.GetAsync<SecuritySettingsDto>(ApiRoutes.AdminSecurity);

        change(settings);

        await admin.PutAsync<SecuritySettingsDto>(ApiRoutes.AdminSecurity, settings);
    }
}
