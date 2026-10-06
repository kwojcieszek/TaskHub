using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;

namespace TaskHub.Tests.Support;

/// <summary>
/// Accepts only the tokens a test registered, in place of the call to Google.
/// </summary>
public class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly Dictionary<string, GoogleUserInfo> _tokens = new();

    public string? ClientId => "test-client";

    public void Register(string idToken, GoogleUserInfo user)
    {
        _tokens[idToken] = user;
    }

    public Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokens.TryGetValue(idToken, out var user) ? user : null);
    }
}
