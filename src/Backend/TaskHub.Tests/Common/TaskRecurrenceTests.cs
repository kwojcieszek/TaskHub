using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Domain.Enums;

namespace TaskHub.Tests.Common;

public class TaskRecurrenceTests
{
    [Theory]
    [InlineData(1, RecurrenceUnit.Day, "2026-03-11")]
    [InlineData(2, RecurrenceUnit.Week, "2026-03-24")]
    [InlineData(1, RecurrenceUnit.Month, "2026-04-10")]
    [InlineData(1, RecurrenceUnit.Year, "2027-03-10")]
    public void FromCompletion_CountsFromTheDayTheTaskWasCompleted(int interval, RecurrenceUnit unit, string expected)
    {
        var recurrence = new TaskRecurrenceDto { Interval = interval, Unit = unit, Basis = RecurrenceBasis.CompletionDate };

        var next = TaskRecurrence.NextDueDate(recurrence, Utc("2026-01-01"), new DateTime(2026, 3, 10, 17, 45, 0, DateTimeKind.Utc));

        Assert.Equal(Utc(expected), next);
    }

    [Fact]
    public void FromDueDate_KeepsTheScheduleWhenCompletedEarly()
    {
        var recurrence = new TaskRecurrenceDto { Interval = 1, Unit = RecurrenceUnit.Week, Basis = RecurrenceBasis.DueDate };

        var next = TaskRecurrence.NextDueDate(recurrence, Utc("2026-03-13"), Utc("2026-03-10"));

        Assert.Equal(Utc("2026-03-20"), next);
    }

    [Fact]
    public void FromDueDate_SkipsDatesThatPassedBeforeTheCompletion()
    {
        var recurrence = new TaskRecurrenceDto { Interval = 1, Unit = RecurrenceUnit.Week, Basis = RecurrenceBasis.DueDate };

        var next = TaskRecurrence.NextDueDate(recurrence, Utc("2026-03-02"), Utc("2026-03-20"));

        Assert.Equal(Utc("2026-03-23"), next);
    }

    [Fact]
    public void FromDueDate_MonthEndScheduleDoesNotDrift()
    {
        var recurrence = new TaskRecurrenceDto { Interval = 1, Unit = RecurrenceUnit.Month, Basis = RecurrenceBasis.DueDate };

        var next = TaskRecurrence.NextDueDate(recurrence, Utc("2026-01-31"), Utc("2026-03-01"));

        Assert.Equal(Utc("2026-03-31"), next);
    }

    [Fact]
    public void FromDueDate_WithoutADueDate_CountsFromTheCompletion()
    {
        var recurrence = new TaskRecurrenceDto { Interval = 3, Unit = RecurrenceUnit.Day, Basis = RecurrenceBasis.DueDate };

        var next = TaskRecurrence.NextDueDate(recurrence, null, Utc("2026-03-10"));

        Assert.Equal(Utc("2026-03-13"), next);
    }

    private static DateTime Utc(string date)
    {
        return DateTime.SpecifyKind(DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
    }
}
