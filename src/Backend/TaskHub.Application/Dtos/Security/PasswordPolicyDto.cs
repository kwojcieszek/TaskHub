namespace TaskHub.Application.Dtos.Security;

public class PasswordPolicyDto
{
    public int MinLength { get; set; } = 8;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireDigit { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; }
}
