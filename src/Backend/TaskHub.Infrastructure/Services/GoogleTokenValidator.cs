using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Services;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options;

    public GoogleTokenValidator(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public string? ClientId => string.IsNullOrWhiteSpace(_options.ClientId) ? null : _options.ClientId.Trim();

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = ClientId;

        if (clientId is null || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            // Checks the signature against Google's keys, the issuer, the expiry and that the token was issued for this application.
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });

            return new GoogleUserInfo
            {
                Subject = payload.Subject,
                Email = payload.Email ?? string.Empty,
                EmailVerified = payload.EmailVerified
            };
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
