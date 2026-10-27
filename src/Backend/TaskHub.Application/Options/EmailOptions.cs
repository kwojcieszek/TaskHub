namespace TaskHub.Application.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public EmailSecurity Security { get; set; } = EmailSecurity.StartTls;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "TaskHub";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
