namespace TaskHub.Application.Dtos.ApiTokens;

public class CreatedApiTokenDto : ApiTokenDto
{
    // The token itself. This is the only answer that ever carries it.
    public string Token { get; set; } = string.Empty;
}
