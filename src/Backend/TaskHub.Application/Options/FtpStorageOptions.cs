namespace TaskHub.Application.Options;

public class FtpStorageOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 21;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string RootPath { get; set; } = "/";

    public bool UseSsl { get; set; }
}
