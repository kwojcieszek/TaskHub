import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { ProjectService } from '../../core/services/project.service';
import { TaskItemService } from '../../core/services/task-item.service';
import { DashboardScope, DashboardSection, DashboardStat } from '../../core/models/dashboard.model';
import { TaskItem, TaskItemPriority } from '../../core/models/task.model';
import { projectColor } from '../../core/utils/project-color';
import { TaskRowComponent } from '../../shared/task-row/task-row.component';

const DAY_MS = 24 * 60 * 60 * 1000;
const UPCOMING_DAYS = 7;
const RECENT_DAYS = 7;
const SECTION_LIMIT = 5;
const PRIORITY_ORDER: Record<TaskItemPriority, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, ConfirmDialog, TranslateModule, TaskRowComponent],
  providers: [ConfirmationService],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly taskItemService = inject(TaskItemService);
  private readonly projectService = inject(ProjectService);
  private readonly language = inject(LanguageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);
  readonly auth = inject(AuthService);

  private readonly tasks = signal<TaskItem[]>([]);
  private readonly projectNames = signal<Record<string, string>>({});
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly scope = signal<DashboardScope>('All');
  readonly scopes: DashboardScope[] = ['All', 'Mine'];
  readonly sectionLimit = SECTION_LIMIT;

  readonly todayLabel = computed(() =>
    new Intl.DateTimeFormat(this.language.current(), { dateStyle: 'full' }).format(new Date())
  );

  // The list is flat, so tasks that have subtasks are recognised by the subtasks pointing at them.
  readonly parentTaskIds = computed(
    () => new Set(this.tasks().map((task) => task.parentTaskId).filter((id): id is string => id !== null))
  );

  // Cancelled tasks never need attention; "Mine" keeps only the tasks assigned to the signed-in user.
  private readonly scopedTasks = computed(() => {
    const userId = this.auth.currentUser()?.id;
    const active = this.tasks().filter((task) => task.status !== 'Cancelled');

    return this.scope() === 'Mine' ? active.filter((task) => task.assignedUserId === userId) : active;
  });

  private readonly openTasks = computed(() => this.scopedTasks().filter((task) => task.status !== 'Done'));

  readonly sections = computed<DashboardSection[]>(() => {
    const today = this.dayStart(Date.now());
    const upcomingEnd = today + (UPCOMING_DAYS + 1) * DAY_MS;
    const open = this.openTasks();

    return [
      {
        key: 'overdue',
        icon: 'pi pi-exclamation-circle',
        tone: 'red',
        tasks: this.byDueDate(open.filter((task) => this.dueDay(task) !== null && this.dueDay(task)! < today))
      },
      {
        key: 'today',
        icon: 'pi pi-sun',
        tone: 'amber',
        tasks: open
          .filter((task) => this.dueDay(task) === today)
          .sort((a, b) => PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority] || a.title.localeCompare(b.title))
      },
      {
        key: 'inProgress',
        icon: 'pi pi-clock',
        tone: 'blue',
        tasks: this.byDueDate(open.filter((task) => task.status === 'InProgress'))
      },
      {
        key: 'upcoming',
        icon: 'pi pi-calendar',
        tone: 'teal',
        tasks: this.byDueDate(
          open.filter((task) => this.dueDay(task) !== null && this.dueDay(task)! > today && this.dueDay(task)! < upcomingEnd)
        )
      }
    ];
  });

  readonly stats = computed<DashboardStat[]>(() => {
    const recentStart = Date.now() - RECENT_DAYS * DAY_MS;
    const completedRecently = this.scopedTasks().filter(
      (task) => task.status === 'Done' && task.completedAt !== null && this.utcTime(task.completedAt) >= recentStart
    ).length;

    return [
      ...this.sections().map((section) => ({
        key: section.key,
        icon: section.icon,
        tone: section.tone,
        value: section.tasks.length
      })),
      { key: 'completedRecently', icon: 'pi pi-check', tone: 'green', value: completedRecently }
    ];
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(false);

    forkJoin({
      tasks: this.taskItemService.getMyTasks(),
      projects: this.projectService.getAll()
    }).subscribe({
      next: ({ tasks, projects }) => {
        this.tasks.set(tasks);
        this.projectNames.set(Object.fromEntries(projects.map((project) => [project.id, project.name])));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      }
    });
  }

  start(task: TaskItem): void {
    // Reloading also on failure brings the row back in line with the server, e.g. when someone else changed the task.
    this.taskItemService.start(task.projectId, task.id).subscribe({ next: () => this.load(), error: () => this.load() });
  }

  complete(task: TaskItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('tasks.completeConfirm'),
      header: this.translate.instant('tasks.completeConfirmHeader'),
      icon: 'pi pi-check-circle',
      acceptLabel: this.translate.instant('common.yes'),
      rejectLabel: this.translate.instant('common.no'),
      accept: () => {
        this.taskItemService
          .complete(task.projectId, task.id)
          .subscribe({ next: () => this.load(), error: () => this.load() });
      }
    });
  }

  projectName(task: TaskItem): string {
    return this.projectNames()[task.projectId] ?? '';
  }

  projectColor(task: TaskItem): string {
    return projectColor(task.projectId);
  }

  // Tasks without a due date go last.
  private byDueDate(tasks: TaskItem[]): TaskItem[] {
    return [...tasks].sort(
      (a, b) =>
        (this.dueDay(a) ?? Number.MAX_SAFE_INTEGER) - (this.dueDay(b) ?? Number.MAX_SAFE_INTEGER) ||
        PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority]
    );
  }

  private dueDay(task: TaskItem): number | null {
    return task.dueDate ? this.dayStart(new Date(task.dueDate).getTime()) : null;
  }

  private dayStart(time: number): number {
    const date = new Date(time);
    date.setHours(0, 0, 0, 0);
    return date.getTime();
  }

  // The API sends these timestamps in UTC but without a zone suffix.
  private utcTime(value: string): number {
    return new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : `${value}Z`).getTime();
  }
}
