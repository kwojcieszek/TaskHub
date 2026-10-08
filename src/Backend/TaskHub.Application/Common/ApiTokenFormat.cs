namespace TaskHub.Application.Common;

public static class ApiTokenFormat
{
    // Tells an API token from a session token at a glance, and lets leaked tokens be searched for.
    public const string Prefix = "thp_";
}
