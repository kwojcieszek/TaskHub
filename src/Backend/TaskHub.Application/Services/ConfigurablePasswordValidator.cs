using Microsoft.AspNetCore.Identity;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Services;

/// <summary>
/// Checks a new password against the policy from the security settings, so a change made in the admin panel
/// applies at once. Passwords that already exist are not affected until they are changed.
/// </summary>
public class ConfigurablePasswordValidator : IPasswordValidator<ApplicationUser>
{
    private readonly ISecuritySettingsService _securitySettingsService;

    public ConfigurablePasswordValidator(ISecuritySettingsService securitySettingsService)
    {
        _securitySettingsService = securitySettingsService;
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        var policy = (await _securitySettingsService.GetAsync()).PasswordPolicy;
        var describer = manager.ErrorDescriber;
        var value = password ?? string.Empty;
        var errors = new List<IdentityError>();

        if (value.Length < policy.MinLength)
        {
            errors.Add(describer.PasswordTooShort(policy.MinLength));
        }

        if (policy.RequireUppercase && !value.Any(char.IsUpper))
        {
            errors.Add(describer.PasswordRequiresUpper());
        }

        if (policy.RequireLowercase && !value.Any(char.IsLower))
        {
            errors.Add(describer.PasswordRequiresLower());
        }

        if (policy.RequireDigit && !value.Any(char.IsDigit))
        {
            errors.Add(describer.PasswordRequiresDigit());
        }

        if (policy.RequireNonAlphanumeric && value.All(char.IsLetterOrDigit))
        {
            errors.Add(describer.PasswordRequiresNonAlphanumeric());
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
    }
}
