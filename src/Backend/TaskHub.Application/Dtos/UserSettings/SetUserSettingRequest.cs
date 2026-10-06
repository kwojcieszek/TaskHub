using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.UserSettings;

public class SetUserSettingRequest
{
    [StringLength(4000)]
    public string Value { get; set; } = string.Empty;
}
