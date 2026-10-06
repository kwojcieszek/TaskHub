namespace TaskHub.Application.Options;

public class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; set; }

    public string[] KnownProxies { get; set; } = Array.Empty<string>();

    public string[] KnownNetworks { get; set; } = Array.Empty<string>();
}
