import { Observable, finalize } from 'rxjs';
import { TaskItemService } from '../../../core/services/task-item.service';
import { TaskHistoryComponent } from '../task-history/task-history.component';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { Button } from 'primeng/button';
import { InputNumber } from 'primeng/inputnumber';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { RecurrenceBasis, RecurrenceUnit, TaskItem, TaskItemPriority, TaskItemStatus, TaskRecurrence } from '../../../core/models/task.model';
import { ProjectMember } from '../../../core/models/project.model';
import { TaskAttachmentsComponent } from '../task-attachments/task-attachments.component';
import { CommentsComponent } from '../../../shared/comments/comments.component';
import { DictationDirective } from '../../../shared/dictation/dictation.directive';

export interface TaskDialogData {
  task?: TaskItem;
  projectId?: string;
  parentTaskId?: string | null;
  // True when the task has subtasks: its status then follows them and cannot be edited.
  statusDerived?: boolean;
  members: ProjectMember[];
}

@Component({
  selector: 'app-task-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DictationDirective, InputText, Textarea, Select, DatePicker, Button, InputNumber, ToggleSwitch, TranslateModule, TaskAttachmentsComponent, CommentsComponent, TaskHistoryComponent],
  templateUrl: './task-dialog.component.html',
  styleUrl: './task-dialog.component.scss'
})
export class TaskDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig<TaskDialogData>);
  private readonly translate = inject(TranslateService);

  readonly data: TaskDialogData = this.config.data ?? { members: [] };

  readonly statuses: TaskItemStatus[] = ['ToDo', 'InProgress', 'Done', 'Cancelled'];
  readonly priorities: TaskItemPriority[] = ['Low', 'Medium', 'High', 'Critical'];

  readonly statusOptions = this.statuses.map((status) => ({
    label: this.translate.instant('tasks.status.' + status),
    value: status
  }));

  readonly priorityOptions = this.priorities.map((priority) => ({
    label: this.translate.instant('tasks.priority.' + priority),
    value: priority
  }));

  readonly recurrenceUnitOptions = (['Day', 'Week', 'Month', 'Year'] as RecurrenceUnit[]).map((unit) => ({
    label: this.translate.instant('tasks.recurrence.unit.' + unit),
    value: unit
  }));

  readonly recurrenceBasisOptions = (['CompletionDate', 'DueDate'] as RecurrenceBasis[]).map((basis) => ({
    label: this.translate.instant('tasks.recurrence.basis.' + basis),
    value: basis
  }));

  // Only a top-level task repeats; its subtasks are copied along with it.
  readonly canRepeat = !(this.data.task?.parentTaskId ?? this.data.parentTaskId);

  readonly assigneeOptions = [
    { label: this.translate.instant('tasks.unassigned'), value: null },
    ...this.data.members.map((member) => ({ label: member.displayName, value: member.userId }))
  ];

  readonly form = this.fb.nonNullable.group({
    title: [this.data.task?.title ?? '', Validators.required],
    description: [this.data.task?.description ?? ''],
    dueDate: [this.data.task?.dueDate ? new Date(this.data.task.dueDate) : null],
    status: [this.data.task?.status ?? 'ToDo'],
    priority: [this.data.task?.priority ?? 'Medium'],
    assignedUserId: [this.data.task?.assignedUserId ?? null],
    repeats: [this.data.task?.recurrence != null],
    recurrenceInterval: [this.data.task?.recurrence?.interval ?? 1, [Validators.min(1), Validators.max(365)]],
    recurrenceUnit: [this.data.task?.recurrence?.unit ?? ('Week' as RecurrenceUnit)],
    recurrenceBasis: [this.data.task?.recurrence?.basis ?? ('CompletionDate' as RecurrenceBasis)]
  });

  private readonly service = inject(TaskItemService);
  readonly saving = signal(false);
  submit(): void {
    if (this.form.invalid) {
      return;
    }

    const { repeats, recurrenceInterval, recurrenceUnit, recurrenceBasis, ...raw } = this.form.getRawValue();
    const recurrence: TaskRecurrence | null = this.canRepeat && repeats
      ? { interval: recurrenceInterval, unit: recurrenceUnit, basis: recurrenceBasis }
      : null;
    const request = {
      ...raw,
      parentTaskId: this.data.task?.parentTaskId ?? this.data.parentTaskId ?? null,
      dueDate: raw.dueDate ? this.toDateOnly(new Date(raw.dueDate)) : null,
      recurrence
    };
    const projectId = this.data.task?.projectId ?? this.data.projectId;
    if (!projectId || this.saving()) return;
    this.saving.set(true);
    const operation: Observable<TaskItem> = this.data.task ? this.service.update(projectId, this.data.task.id, request) : this.service.create(projectId, request);
    operation.pipe(finalize(() => this.saving.set(false))).subscribe({next: () => this.dialogRef.close(true), error: () => {}});
  }

  // The due date is a calendar day, so the picked day is sent as it is; converting local midnight to UTC
  // would move it to the previous day in time zones ahead of UTC.
  private toDateOnly(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');

    return `${date.getFullYear()}-${month}-${day}T00:00:00Z`;
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
