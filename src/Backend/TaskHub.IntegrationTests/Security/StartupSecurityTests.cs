using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using TaskHub.Domain.Entities;
using TaskHub.Application.Common;
using TaskHub.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TaskHub.Api;
namespace TaskHub.IntegrationTests.Security;

public class StartupSecurityTests
{
    [Theory]
    [InlineData("CHANGE_ME_TO_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS", "PrivateAdmin123!", "FileSystem", false, false)]
    [InlineData("short", "PrivateAdmin123!", "FileSystem", false, false)]
    [InlineData("private-signing-secret-at-least-32-bytes", "admin", "FileSystem", false, false)]
    [InlineData("private-signing-secret-at-least-32-bytes", "PrivateAdmin123!", "Ftp", false, false)]
    [InlineData("private-signing-secret-at-least-32-bytes", "PrivateAdmin123!", "Ftp", true, true)]
    [InlineData("private-signing-secret-at-least-32-bytes", "PrivateAdmin123!", "FileSystem", false, true)]
    public void ProductionRejectsUnsafeConfiguration(string key, string password, string provider, bool ssl, bool valid)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Jwt:SigningKey"] = key, ["DefaultAdmin:Password"] = password,
            ["AttachmentStorage:Provider"] = provider, ["AttachmentStorage:Ftp:UseSsl"] = ssl.ToString()
        }).Build();
        if (valid) StartupSecurity.Validate(config, new EnvironmentStub());
        else Assert.Throws<InvalidOperationException>(() => StartupSecurity.Validate(config, new EnvironmentStub()));
    }
    [Fact]
    public async Task ProductionAlsoRejectsAnExistingAdministratorWithTheDefaultPassword()
    {
        using var factory = new TaskHubApiFactory();
        await factory.AdminAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        await StartupSecurity.ValidateAccountsAsync(users);
        var user = new User { Id = Guid.NewGuid(), UserName = "legacyadmin", DisplayName = "Legacy", Email = "legacyadmin@example.test" };
        user.PasswordHash = users.PasswordHasher.HashPassword(user, "admin");
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        Assert.True((await users.AddToRoleAsync(user, Roles.Admin)).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartupSecurity.ValidateAccountsAsync(users));
    }

    private class EnvironmentStub : IHostEnvironment {
        public string EnvironmentName {get;set;} = Environments.Production;
        public string ApplicationName {get;set;} = "Tests";
        public string ContentRootPath {get;set;} = ".";
        public IFileProvider ContentRootFileProvider {get;set;} = new NullFileProvider();
    }
}
