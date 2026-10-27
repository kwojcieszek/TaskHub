using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Common;

/// <summary>Works out when the next occurrence of a repeating task is due.</summary>
public static class TaskRecurrence
{
    public const int MinInterval = 1;
    public const int MaxInterval = 365;

    public static DateTime NextDueDate(TaskRecurrenceDto recurrence, DateTime? dueDate, DateTime completedAt)
    {
        var completedOn = DateTime.SpecifyKind(completedAt.Date, DateTimeKind.Utc);

        if (recurrence.Basis == RecurrenceBasis.CompletionDate || dueDate is null)
        {
            return Advance(completedOn, recurrence, 1);
        }

        // Every date is counted from the original one, so a month-end schedule does not drift to the 28th.
        // A schedule that fell behind continues with its first date after the completion day, without a backlog of copies.
        var steps = 1;
        var next = Advance(dueDate.Value, recurrence, steps);

        while (next <= completedOn)
        {
            steps++;
            next = Advance(dueDate.Value, recurrence, steps);
        }

        return next;
    }

    private static DateTime Advance(DateTime date, TaskRecurrenceDto recurrence, int steps)
    {
        var amount = recurrence.Interval * steps;

        return recurrence.Unit switch
        {
            RecurrenceUnit.Day => date.AddDays(amount),
            RecurrenceUnit.Week => date.AddDays(7 * amount),
            RecurrenceUnit.Month => date.AddMonths(amount),
            RecurrenceUnit.Year => date.AddYears(amount),
            _ => throw new ArgumentOutOfRangeException(nameof(recurrence), recurrence.Unit, "Unknown recurrence unit.")
        };
    }
}
