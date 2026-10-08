namespace TaskHub.Api.Authentication;

public static class ApiTokenAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiToken";

    // Looks at the request and hands it to the scheme that understands the credential it carries.
    public const string SelectorScheme = "SessionOrApiToken";
}
