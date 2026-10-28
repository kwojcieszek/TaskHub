import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { TaskItem, TaskItemStatus } from '../../core/models/task.model';
import { LocalizedDatePipe } from '../pipes/localized-date.pipe';
import { TaskDescriptionPreviewComponent } from '../task-description-preview/task-description-preview.component';

const STATUS_ICONS: Record<TaskItemStatus, string> = {
  ToDo: 'pi pi-circle',
  InProgress: 'pi pi-clock',
  Done: 'pi pi-check',
  Cancelled: 'pi pi-times'
};

@Component({
  selector: 'app-task-row',
  standalone: true,
  imports: [LocalizedDatePipe, TaskDescriptionPreviewComponent, TranslateModule],
  templateUrl: './task-row.component.html',
  styleUrl: './task-row.component.scss'
})
export class TaskRowComponent {
  @Input({ required: true }) task!: TaskItem;
  @Input() projectName: string | null = null;
  @Input() projectColor: string | null = null;
  @Input() canEdit = true;
  @Input() allowSubTasks = false;
  // Leaves only the start, finish and reopen buttons: no editing on click and no delete.
  @Input() progressOnly = false;
  // Shows a subtask like an ordinary row, for lists where its parent is not displayed above it.
  @Input() flat = false;
  // Places metadata beneath the title in dashboard cards.
  @Input() stacked = false;
  // Flat task lists carry no nested subtasks, so the caller says whether the task has any.
  @Input() hasSubTasks: boolean | null = null;

  @Output() readonly edit = new EventEmitter<void>();
  @Output() readonly start = new EventEmitter<void>();
  @Output() readonly complete = new EventEmitter<void>();
  @Output() readonly reopen = new EventEmitter<void>();
  @Output() readonly remove = new EventEmitter<void>();
  @Output() readonly addSubTask = new EventEmitter<void>();

  get isSubTask(): boolean {
    return this.task.parentTaskId !== null;
  }

  get isNested(): boolean {
    return this.isSubTask && !this.flat;
  }

  // A task with subtasks is started and completed through them, never directly.
  get isStatusDerived(): boolean {
    return this.hasSubTasks ?? this.task.subTasks.length > 0;
  }

  // Cancelled subtasks do not count towards the task, the same way the API derives its status.
  get activeSubTaskCount(): number {
    return this.task.subTasks.filter((subTask) => subTask.status !== 'Cancelled').length;
  }

  get doneSubTaskCount(): number {
    return this.task.subTasks.filter((subTask) => subTask.status === 'Done').length;
  }

  get statusIcon(): string {
    return STATUS_ICONS[this.task.status];
  }

  get isClosed(): boolean {
    return this.task.status === 'Done' || this.task.status === 'Cancelled';
  }

  get isOverdue(): boolean {
    if (!this.task.dueDate || this.isClosed) {
      return false;
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    return new Date(this.task.dueDate) < today;
  }

  get hasDescription(): boolean {
    return !!this.task.description?.trim();
  }

  get isClickable(): boolean {
    return this.canEdit && !this.progressOnly;
  }

  onRowClick(): void {
    if (this.isClickable) {
      this.edit.emit();
    }
  }

  emit(emitter: EventEmitter<void>, event: Event): void {
    event.stopPropagation();
    emitter.emit();
  }
}
