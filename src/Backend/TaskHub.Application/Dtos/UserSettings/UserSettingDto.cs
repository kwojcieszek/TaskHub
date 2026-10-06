namespace TaskHub.Application.Dtos.UserSettings;

public class UserSettingDto
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
