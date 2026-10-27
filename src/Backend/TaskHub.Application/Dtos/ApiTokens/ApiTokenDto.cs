namespace TaskHub.Application.Dtos.ApiTokens;

public class ApiTokenDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Empty for a token that never expires.
    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public bool Expired { get; set; }
}
