namespace TaskHub.Api.Authentication;

public static class AuthorizationPolicies
{
    // Only a signed-in session may do this, not an API token: managing tokens, the password and the second factor.
    public const string InteractiveSession = "InteractiveSession";
}
