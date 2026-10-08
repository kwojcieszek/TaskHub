using System.ComponentModel.DataAnnotations;

namespace TaskHub.Application.Dtos.Comments;

public class SaveCommentRequest
{
    public const int MaxContentLength = 4000;

    [Required]
    [StringLength(MaxContentLength)]
    public string Content { get; set; } = string.Empty;
}
