namespace TaskHub.Application.Common;

public static class AppClaimTypes
{
    // Version of the user's sessions; a token carrying an older value than the user has now is rejected.
    public const string TokenVersion = "tv";
    public const string SessionId = "sid";

    // Marks a short-lived token that only proves the first login step; it is never a valid access token.
    public const string Purpose = "purpose";

    // Added to the signed-in principal while the user still has to replace an assigned password.
    public const string PasswordChangeRequired = "pwd_change";

    // Carried by a request signed in with an API token instead of a session; holds the id of that token.
    public const string ApiTokenId = "api_token";
}
