using OtpNet;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Plays the authenticator app: turns the shared key of a 2FA setup into the current six-digit code.
/// </summary>
public static class Authenticator
{
    public static string CurrentCode(string sharedKey)
    {
        var totp = new Totp(Base32Encoding.ToBytes(sharedKey));

        return totp.ComputeTotp(DateTime.UtcNow);
    }

    /// <summary>A well-formed code that is certainly not the current one.</summary>
    public static string WrongCode(string sharedKey)
    {
        var current = int.Parse(CurrentCode(sharedKey));

        // Far from the neighbouring time steps the server also accepts is not guaranteed by arithmetic alone,
        // but a collision with one of those few codes is a one-in-a-hundred-thousand event.
        return ((current + 500_000) % 1_000_000).ToString("D6");
    }
}
