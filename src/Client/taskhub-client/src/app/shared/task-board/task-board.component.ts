import { CdkDrag, CdkDragDrop, CdkDragPlaceholder, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { DEFAULT_TASK_BOARD_STATUSES, TaskBoardColumn, TaskBoardMove, TaskBoardStatus } from '../../core/models/task-board.model';
import { TaskItem } from '../../core/models/task.model';
import { projectColor } from '../../core/utils/project-color';
import { LocalizedDatePipe } from '../pipes/localized-date.pipe';
import { TaskDescriptionPreviewComponent } from '../task-description-preview/task-description-preview.component';

const COLUMNS: { status: TaskBoardStatus; icon: string }[] = [
  { status: 'ToDo', icon: 'pi pi-circle' },
  { status: 'InProgress', icon: 'pi pi-play' },
  { status: 'Done', icon: 'pi pi-check-circle' },
  { status: 'Cancelled', icon: 'pi pi-ban' }
];

// A column with years of finished tasks would bury the board; the newest ones are enough.
const MAX_CARDS_PER_COLUMN = 60;

/**
 * Tasks as a kanban board: one column per status, cards dragged between columns to change it.
 * The board only reports a move; whoever shows it decides how the status is changed.
 */
@Component({
  selector: 'app-task-board',
  standalone: true,
  imports: [CdkDropListGroup, CdkDropList, CdkDrag, CdkDragPlaceholder, LocalizedDatePipe, TaskDescriptionPreviewComponent, TranslateModule],
  templateUrl: './task-board.component.html',
  styleUrl: './task-board.component.scss'
})
export class TaskBoardComponent {
  private readonly taskList = signal<TaskItem[]>([]);
  private readonly visibleStatuses = signal<TaskBoardStatus[]>(DEFAULT_TASK_BOARD_STATUSES);

  @Input({ required: true }) set tasks(value: TaskItem[]) {
    this.taskList.set(value);
  }

  // The statuses that get a column. Their order on the board is always the order of the workflow.
  @Input() set statuses(value: TaskBoardStatus[]) {
    this.visibleStatuses.set(value);
  }

  // Tasks whose status follows their subtasks; they cannot be dragged.
  @Input() lockedTaskIds: ReadonlySet<string> = new Set<string>();
  @Input() projectNames: Record<string, string> = {};

  @Output() readonly open = new EventEmitter<TaskItem>();
  @Output() readonly move = new EventEmitter<TaskBoardMove>();

  readonly columns = computed<TaskBoardColumn[]>(() =>
    COLUMNS.filter((column) => this.visibleStatuses().includes(column.status)).map((column) => {
      const tasks = this.taskList()
        .filter((task) => task.status === column.status)
        .sort((a, b) => this.sortKey(a, column.status) - this.sortKey(b, column.status));

      return {
        status: column.status,
        icon: column.icon,
        tasks: tasks.slice(0, MAX_CARDS_PER_COLUMN),
        hidden: Math.max(tasks.length - MAX_CARDS_PER_COLUMN, 0)
      };
    })
  );

  onDrop(event: CdkDragDrop<TaskBoardStatus, TaskBoardStatus, TaskItem>): void {
    const task = event.item.data;
    const status = event.container.data;

    if (event.previousContainer === event.container || task.status === status) {
      return;
    }

    this.move.emit({ task, status });
  }

  isLocked(task: TaskItem): boolean {
    return this.lockedTaskIds.has(task.id);
  }

  isOverdue(task: TaskItem): boolean {
    if (!task.dueDate || task.status === 'Done' || task.status === 'Cancelled') {
      return false;
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    return new Date(task.dueDate) < today;
  }

  projectName(task: TaskItem): string {
    return this.projectNames[task.projectId] ?? '';
  }

  projectColor(task: TaskItem): string {
    return projectColor(task.projectId);
  }

  initial(task: TaskItem): string {
    return (task.assignedUserDisplayName ?? '').charAt(0).toUpperCase();
  }

  // Open tasks by due date, the ones without a date last; closed tasks with the latest first.
  private sortKey(task: TaskItem, status: TaskBoardStatus): number {
    if (status === 'Done' || status === 'Cancelled') {
      return -new Date(task.completedAt ?? task.updatedAt ?? task.createdAt).getTime();
    }

    return task.dueDate ? new Date(task.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
  }
}
