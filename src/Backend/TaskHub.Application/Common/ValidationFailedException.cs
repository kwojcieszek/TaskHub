namespace TaskHub.Application.Common;

public class ValidationFailedException : Exception
{
    public IReadOnlyCollection<string> Errors { get; }

    public ValidationFailedException(IEnumerable<string> errors) : base("Validation failed.")
    {
        Errors = errors.ToList();
    }
}
