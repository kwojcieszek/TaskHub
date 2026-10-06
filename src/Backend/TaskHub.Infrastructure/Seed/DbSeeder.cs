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
                DisplayName = "Administrator"
            };

            var result = await _userManager.CreateAsync(admin, _defaultAdmin.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, Roles.Admin);
            }
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
