using TaskHub.Application.Dtos.Security;

namespace TaskHub.Application.Dtos.Auth;

public class AuthProvidersDto
{
    public string? GoogleClientId { get; set; }

    public bool RegistrationEnabled { get; set; }

    // Public, so the password forms can show the rules before anything is submitted.
    public PasswordPolicyDto PasswordPolicy { get; set; } = new();
}
