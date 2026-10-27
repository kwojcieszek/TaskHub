using System.ComponentModel.DataAnnotations;
using TaskHub.Application.Common;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Dtos.Tasks;

public class TaskRecurrenceDto
{
    [Range(TaskRecurrence.MinInterval, TaskRecurrence.MaxInterval)]
    public int Interval { get; set; } = 1;

    public RecurrenceUnit Unit { get; set; } = RecurrenceUnit.Week;

    public RecurrenceBasis Basis { get; set; } = RecurrenceBasis.CompletionDate;
}
