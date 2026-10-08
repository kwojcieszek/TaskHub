namespace TaskHub.Domain.Entities;

public class ApiToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;

    // SHA-256 of the token; the token itself is shown once when it is created and never stored.
    public string TokenHash { get; set; } = string.Empty;

    // The first characters of the token, so its owner can tell their tokens apart.
    public string Prefix { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Empty for a token that never expires.
    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
