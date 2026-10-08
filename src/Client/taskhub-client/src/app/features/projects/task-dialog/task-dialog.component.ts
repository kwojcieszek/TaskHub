import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { Button } from 'primeng/button';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TaskItem, TaskItemPriority, TaskItemStatus } from '../../../core/models/task.model';
import { ProjectMember } from '../../../core/models/project.model';
import { TaskAttachmentsComponent } from '../task-attachments/task-attachments.component';
import { CommentsComponent } from '../../../shared/comments/comments.component';
import { DictationDirective } from '../../../shared/dictation/dictation.directive';

export interface TaskDialogData {
  task?: TaskItem;
  // True when the task has subtasks: its status then follows them and cannot be edited.
  statusDerived?: boolean;
  members: ProjectMember[];
}

@Component({
  selector: 'app-task-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DictationDirective, InputText, Textarea, Select, DatePicker, Button, TranslateModule, TaskAttachmentsComponent, CommentsComponent],
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
    assignedUserId: [this.data.task?.assignedUserId ?? null]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();
    this.dialogRef.close({
      ...raw,
      parentTaskId: this.data.task?.parentTaskId ?? null,
      dueDate: raw.dueDate ? this.toDateOnly(new Date(raw.dueDate)) : null
    });
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
