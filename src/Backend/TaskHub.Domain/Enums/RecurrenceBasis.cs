namespace TaskHub.Domain.Enums;

/// <summary>The date the interval of a repeating task is counted from.</summary>
public enum RecurrenceBasis
{
    CompletionDate = 0,
    DueDate = 1
}
