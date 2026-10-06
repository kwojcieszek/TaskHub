namespace TaskHub.Infrastructure.Storage;

internal static class AttachmentStoragePath
{
    /// <summary>
    /// Spreads files over two directory levels (ab/cd/abcd...) so a single directory never grows too large.
    /// </summary>
    public static string[] Segments(string sha256)
    {
        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Storage key must be a 64-character hexadecimal SHA-256 hash.", nameof(sha256));
        }

        var key = sha256.ToLowerInvariant();

        return new[] { key[..2], key.Substring(2, 2), key };
    }
}
