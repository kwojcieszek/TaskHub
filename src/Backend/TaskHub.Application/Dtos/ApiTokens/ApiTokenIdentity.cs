namespace TaskHub.Application.Dtos.ApiTokens;

/// <summary>
/// The user a valid API token stands for, as the user is right now: roles are read when the token is used.
/// </summary>
public class ApiTokenIdentity
{
    public Guid TokenId { get; set; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = new List<string>();
}
