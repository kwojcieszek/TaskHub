namespace TaskHub.Application.Options;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int WindowSeconds { get; set; } = 60;

    public int PermitLimit { get; set; } = 300;

    public int AuthPermitLimit { get; set; } = 10;
}
