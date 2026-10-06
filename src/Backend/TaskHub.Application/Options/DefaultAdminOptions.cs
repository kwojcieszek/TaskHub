namespace TaskHub.Application.Options;

public class DefaultAdminOptions
{
    public const string SectionName = "DefaultAdmin";

    public string UserName { get; set; } = "admin";

    public string Password { get; set; } = "admin";

    public string Email { get; set; } = "admin@taskhub.local";
}
