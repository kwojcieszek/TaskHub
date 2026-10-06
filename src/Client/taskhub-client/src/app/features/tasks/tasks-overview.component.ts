import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { TaskItemService } from '../../core/services/task-item.service';
import { ProjectService } from '../../core/services/project.service';
import { TaskItem } from '../../core/models/task.model';
import { TaskStatusFilter } from '../../core/models/task-status-filter.model';
import { projectColor } from '../../core/utils/project-color';
import { StatusFilterComponent } from '../../shared/status-filter/status-filter.component';
import { TaskRowComponent } from '../../shared/task-row/task-row.component';
import { TaskDialogComponent } from '../projects/task-dialog/task-dialog.component';

interface TaskSection {
  key: string;
  icon: string;
  tone: string;
  tasks: TaskItem[];
}

@Component({
  selector: 'app-tasks-overview',
  standalone: true,
  imports: [ConfirmDialog, TranslateModule, TaskRowComponent, StatusFilterComponent],
  providers: [DialogService, ConfirmationService],
  templateUrl: './tasks-overview.component.html',
  styleUrl: './tasks-overview.component.scss'
})
export class TasksOverviewComponent implements OnInit {
  private readonly taskItemService = inject(TaskItemService);
  private readonly projectService = inject(ProjectService);
  private readonly dialogService = inject(DialogService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  private readonly tasks = signal<TaskItem[]>([]);
  private readonly projectNames = signal<Record<string, string>>({});
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly statusFilter = signal<TaskStatusFilter>('ToDo');

  // The list is flat, so tasks that have subtasks are recognised by the subtasks pointing at them.
  readonly parentTaskIds = computed(
    () => new Set(this.tasks().map((task) => task.parentTaskId).filter((id): id is string => id !== null))
  );

  readonly overdue = computed(() => this.bucket((date, today) => date < today, true));
  readonly today = computed(() => this.bucket((date, today) => date.getTime() === today.getTime(), false));
  readonly upcoming = computed(() => this.bucket((date, today) => date.getTime() > today.getTime(), true));

  readonly noDueDate = computed(() =>
    this.tasks()
      .filter((task) => !task.dueDate && this.matchesStatus(task, true))
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
  );

  readonly sections = computed<TaskSection[]>(() => [
    { key: 'overdue', icon: 'pi pi-exclamation-circle', tone: 'red', tasks: this.overdue() },
    { key: 'today', icon: 'pi pi-sun', tone: 'amber', tasks: this.today() },
    { key: 'upcoming', icon: 'pi pi-calendar', tone: 'blue', tasks: this.upcoming() },
    { key: 'noDueDate', icon: 'pi pi-inbox', tone: 'slate', tasks: this.noDueDate() }
  ]);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(false);

    forkJoin({
      tasks: this.taskItemService.getAcrossProjects(),
      projects: this.projectService.getAll()
    }).subscribe({
      next: ({ tasks, projects }) => {
        this.tasks.set(tasks);
        this.projectNames.set(Object.fromEntries(projects.map((p) => [p.id, p.name])));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      }
    });
  }

  projectName(task: TaskItem): string {
    return this.projectNames()[task.projectId] ?? '';
  }

  projectColor(task: TaskItem): string {
    return projectColor(task.projectId);
  }

  edit(task: TaskItem): void {
    this.projectService.getMembers(task.projectId).subscribe((members) => {
      const ref = this.dialogService.open(TaskDialogComponent, {
        header: this.translate.instant('common.edit'),
        width: '480px',
        data: { task, members, statusDerived: this.parentTaskIds().has(task.id) }
      });
      ref?.onClose.subscribe((result) => {
        if (!result) {
          // Attachments are saved straight from the dialog, so the row may have changed even on cancel.
          this.load();
          return;
        }
        this.taskItemService.update(task.projectId, task.id, result).subscribe(() => this.load());
      });
    });
  }

  start(task: TaskItem): void {
    this.taskItemService.start(task.projectId, task.id).subscribe(() => this.load());
  }

  complete(task: TaskItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('tasks.completeConfirm'),
      header: this.translate.instant('tasks.completeConfirmHeader'),
      icon: 'pi pi-check-circle',
      acceptLabel: this.translate.instant('common.yes'),
      rejectLabel: this.translate.instant('common.no'),
      accept: () => {
        this.taskItemService.complete(task.projectId, task.id).subscribe(() => this.load());
      }
    });
  }

  reopen(task: TaskItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('tasks.reopenConfirm'),
      header: this.translate.instant('tasks.reopenConfirmHeader'),
      icon: 'pi pi-undo',
      acceptLabel: this.translate.instant('common.yes'),
      rejectLabel: this.translate.instant('common.no'),
      accept: () => {
        this.taskItemService.reopen(task).subscribe(() => this.load());
      }
    });
  }

  delete(task: TaskItem): void {
    this.confirmDelete('tasks.deleteConfirm', () => {
      this.taskItemService.delete(task.projectId, task.id).subscribe(() => this.load());
    });
  }

  // With no filter the sections keep their usual rules; a chosen status shows exactly the tasks in that status.
  private matchesStatus(task: TaskItem, excludeDone: boolean): boolean {
    const filter = this.statusFilter();

    if (filter !== 'All') {
      return task.status === filter;
    }

    return task.status !== 'Cancelled' && (!excludeDone || task.status !== 'Done');
  }

  private bucket(predicate: (date: Date, today: Date) => boolean, excludeDone: boolean): TaskItem[] {
    const todayStart = new Date();
    todayStart.setHours(0, 0, 0, 0);

    return this.tasks()
      .filter((task) => task.dueDate && this.matchesStatus(task, excludeDone))
      .filter((task) => {
        const dueDate = new Date(task.dueDate!);
        dueDate.setHours(0, 0, 0, 0);
        return predicate(dueDate, todayStart);
      })
      .sort((a, b) => new Date(a.dueDate!).getTime() - new Date(b.dueDate!).getTime());
  }

  private confirmDelete(messageKey: string, accept: () => void): void {
    this.confirmationService.confirm({
      message: this.translate.instant(messageKey),
      header: this.translate.instant('common.deleteConfirmHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('common.delete'),
      rejectLabel: this.translate.instant('common.cancel'),
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept
    });
  }
}
