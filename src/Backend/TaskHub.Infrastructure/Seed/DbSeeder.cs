using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskHub.Application.Common;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;
using TaskHub.Infrastructure.Persistence;

namespace TaskHub.Infrastructure.Seed;

public class DbSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly DefaultAdminOptions _defaultAdmin;

    public DbSeeder(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<DefaultAdminOptions> defaultAdmin)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _defaultAdmin = defaultAdmin.Value;
    }

    public async Task SeedAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();

        await EnsureRoleAsync(Roles.Admin);
        await EnsureRoleAsync(Roles.User);

        if (!_userManager.Users.AsNoTracking().Any())
        {
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = _defaultAdmin.UserName,
                Email = _defaultAdmin.Email,
                EmailConfirmed = true,
                DisplayName = "Administrator",
                MustChangePassword = true
            };

            // The starting password is exempt from the password policy: it only lets the administrator in
            // to set a real one, which the policy then applies to.
            admin.PasswordHash = _userManager.PasswordHasher.HashPassword(admin, _defaultAdmin.Password);

            var result = await _userManager.CreateAsync(admin);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, Roles.Admin);
            }
        }

        await RequireDefaultPasswordChangeAsync();
    }

    // A database created before password changes existed may still have the administrator on the published default password.
    private async Task RequireDefaultPasswordChangeAsync()
    {
        var admin = await _userManager.FindByNameAsync(_defaultAdmin.UserName);

        if (admin is null || admin.MustChangePassword)
        {
            return;
        }

        if (await _userManager.CheckPasswordAsync(admin, _defaultAdmin.Password))
        {
            admin.MustChangePassword = true;
            await _userManager.UpdateAsync(admin);
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new ApplicationRole(roleName));
        }
    }
}
