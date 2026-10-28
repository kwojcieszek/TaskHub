import { FormsModule } from '@angular/forms';
import { UserSettingService } from '../../core/services/user-setting.service';
import { ApiFeedbackService } from '../../core/services/api-feedback.service';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of } from 'rxjs';
import { TaskItemService } from '../../core/services/task-item.service';
import { ProjectService } from '../../core/services/project.service';
import { DEFAULT_TASK_BOARD_STATUSES, TASK_BOARD_STATUSES, TaskBoardMove, TaskBoardStatus } from '../../core/models/task-board.model';
import { TaskItem } from '../../core/models/task.model';
import { TaskStatusFilter } from '../../core/models/task-status-filter.model';
import { projectColor } from '../../core/utils/project-color';
import { StatusFilterComponent } from '../../shared/status-filter/status-filter.component';
import { TaskBoardComponent } from '../../shared/task-board/task-board.component';
import { TaskRowComponent } from '../../shared/task-row/task-row.component';
import { TaskDialogComponent } from '../projects/task-dialog/task-dialog.component';

type TasksView = 'list' | 'board';

const VIEW_STORAGE_KEY = 'taskhub.tasksView';
const BOARD_STATUSES_STORAGE_KEY = 'taskhub.boardStatuses';

interface SavedFilter { name: string; search: string; project: string; priority: string; status: TaskStatusFilter; }

interface TaskSection {
  key: string;
  icon: string;
  tone: string;
  tasks: TaskItem[];
}

@Component({
  selector: 'app-tasks-overview',
  standalone: true,
  imports: [FormsModule, ConfirmDialog, Button, InputText, Select, TranslateModule, TaskRowComponent, TaskBoardComponent, StatusFilterComponent],
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

  private readonly settings = inject(UserSettingService);
  private readonly feedback = inject(ApiFeedbackService);
  readonly search = signal(''); readonly projectFilter = signal(''); readonly priorityFilter = signal('');
  readonly filterName = signal(''); readonly savedFilters = signal<SavedFilter[]>([]);
  // Phones start with the filters folded away; wider screens always show them.
  readonly filtersOpen = signal(false);
  readonly hasActiveFilters = computed(() => !!this.search().trim() || !!this.projectFilter() || !!this.priorityFilter());
  readonly projectOptions = computed(() => Object.entries(this.projectNames()).map(([id, name]) => ({id, name})));

  // An empty value means no filter; it is the first choice of both lists.
  readonly projectFilterOptions = computed(() => [
    { label: this.translate.instant('filters.all'), value: '' },
    ...this.projectOptions().map((project) => ({ label: project.name, value: project.id }))
  ]);

  readonly priorityFilterOptions = [
    { label: this.translate.instant('filters.all'), value: '' },
    ...['Low', 'Medium', 'High', 'Critical'].map((priority) => ({
      label: this.translate.instant('tasks.priority.' + priority),
      value: priority
    }))
  ];
  readonly filteredTasks = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return this.tasks().filter(task => (!this.projectFilter() || task.projectId === this.projectFilter()) &&
      (!this.priorityFilter() || task.priority === this.priorityFilter()) &&
      (!query || [task.title, task.description, task.assignedUserDisplayName, this.projectName(task)].join(' ').toLocaleLowerCase().includes(query)));
  });
  saveFilter(): void {
    const name = this.filterName().trim().slice(0, 60); if (!name) return;
    const filter: SavedFilter = {name, search: this.search().slice(0, 200), project: this.projectFilter(), priority: this.priorityFilter(), status: this.statusFilter()};
    this.persistFilters([...this.savedFilters().filter(f => f.name !== name), filter].slice(-10));
  }
  applyFilter(filter: SavedFilter): void { this.search.set(filter.search); this.projectFilter.set(filter.project); this.priorityFilter.set(filter.priority); this.statusFilter.set(filter.status); }
  deleteFilter(filter: SavedFilter): void { this.persistFilters(this.savedFilters().filter(f => f.name !== filter.name)); }
  private persistFilters(filters: SavedFilter[]): void {
    const value = JSON.stringify(filters);
    if(value.length > 4000) {this.feedback.show('error', 'filters.tooLong'); return;}
    this.settings.set('taskFilters', value).subscribe({next: () => {this.savedFilters.set(filters); this.filterName.set('');}, error: () => {}});
  }
  private readonly tasks = signal<TaskItem[]>([]);
  readonly projectNames = signal<Record<string, string>>({});
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly statusFilter = signal<TaskStatusFilter>('ToDo');
  readonly moveError = signal(false);

  // The list groups tasks by due date; the board groups them by status. The choice is remembered.
  readonly view = signal<TasksView>(localStorage.getItem(VIEW_STORAGE_KEY) === 'board' ? 'board' : 'list');

  // The board decides by its columns which tasks it shows, so it gets all of them.
  readonly boardTasks = computed(() => this.filteredTasks());

  // The statuses that have a column on the board; any combination with at least one. The choice is remembered.
  readonly boardStatusOptions = TASK_BOARD_STATUSES;
  readonly boardStatuses = signal<TaskBoardStatus[]>(this.readBoardStatuses());

  // The list is flat, so tasks that have subtasks are recognised by the subtasks pointing at them.
  readonly parentTaskIds = computed(
    () => new Set(this.tasks().map((task) => task.parentTaskId).filter((id): id is string => id !== null))
  );

  readonly overdue = computed(() => this.bucket((date, today) => date < today, true));
  readonly today = computed(() => this.bucket((date, today) => date.getTime() === today.getTime(), false));
  readonly upcoming = computed(() => this.bucket((date, today) => date.getTime() > today.getTime(), true));

  readonly noDueDate = computed(() =>
    this.filteredTasks()
      .filter((task) => !task.dueDate && this.matchesStatus(task, true))
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
  );

  readonly sections = computed<TaskSection[]>(() => [
    { key: 'overdue', icon: 'pi pi-exclamation-circle', tone: 'red', tasks: this.overdue() },
    { key: 'today', icon: 'pi pi-sun', tone: 'green', tasks: this.today() },
    { key: 'upcoming', icon: 'pi pi-calendar', tone: 'blue', tasks: this.upcoming() },
    { key: 'noDueDate', icon: 'pi pi-inbox', tone: 'slate', tasks: this.noDueDate() }
  ]);

  ngOnInit(): void {
    this.settings.getAll().subscribe({next: settings => {
      try {
        const parsed: unknown = JSON.parse(settings.find(s => s.key === 'taskFilters')?.value ?? '[]');
        if (Array.isArray(parsed)) this.savedFilters.set(parsed.filter(f => f && typeof f.name === 'string' && typeof f.search === 'string' && typeof f.project === 'string' && ['', 'Low', 'Medium', 'High', 'Critical'].includes(f.priority) && ['All', 'ToDo', 'InProgress', 'Done'].includes(f.status)).slice(0, 10));
      } catch { this.savedFilters.set([]); }
    }, error: () => {}});
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

  setView(view: TasksView): void {
    this.view.set(view);
    localStorage.setItem(VIEW_STORAGE_KEY, view);
  }

  showsBoardStatus(status: TaskBoardStatus): boolean {
    return this.boardStatuses().includes(status);
  }

  toggleBoardStatus(status: TaskBoardStatus): void {
    const chosen = this.boardStatuses();
    const next = chosen.includes(status) ? chosen.filter((item) => item !== status) : [...chosen, status];

    // A board without columns shows nothing, so the last status stays on.
    if (next.length === 0) {
      return;
    }

    // Kept in the order of the workflow, whatever the order of the clicks.
    const ordered = TASK_BOARD_STATUSES.filter((item) => next.includes(item));

    this.boardStatuses.set(ordered);
    localStorage.setItem(BOARD_STATUSES_STORAGE_KEY, JSON.stringify(ordered));
  }

  // A card was dropped into another column of the board.
  moveTask(move: TaskBoardMove): void {
    this.moveError.set(false);

    // A slip of the hand while dragging must not change a task; until confirmed the card stays where it was.
    this.confirmationService.confirm({
      message: this.translate.instant('tasksOverview.moveConfirm', {
        title: move.task.title,
        status: this.translate.instant('tasks.status.' + move.status)
      }),
      header: this.translate.instant('tasksOverview.moveConfirmHeader'),
      icon: 'pi pi-arrows-h',
      acceptLabel: this.translate.instant('common.yes'),
      rejectLabel: this.translate.instant('common.no'),
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.applyMove(move)
    });
  }

  private applyMove(move: TaskBoardMove): void {
    this.taskItemService.setStatus(move.task, move.status).subscribe({
      next: () => this.load(),
      error: () => {
        // Refused, for example for a viewer of the project: the card goes back where it was.
        this.moveError.set(true);
        this.load();
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
    // The task must open even when the list of people to assign it to cannot be loaded.
    const members$ = this.projectService.getMembers(task.projectId).pipe(catchError(() => of([])));

    members$.subscribe((members) => {
      const ref = this.dialogService.open(TaskDialogComponent, {
        header: this.translate.instant('common.edit'),
        width: 'min(620px, 96vw)',
        style: { maxHeight: 'calc(100dvh - 2rem)' },
        data: { task, members, statusDerived: this.parentTaskIds().has(task.id) }
      });
      ref?.onClose.subscribe((result) => {
        if (!result) {
          // Attachments are saved straight from the dialog, so the row may have changed even on cancel.
          this.load();
          return;
        }
        this.load();
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

  private readBoardStatuses(): TaskBoardStatus[] {
    try {
      const stored: unknown = JSON.parse(localStorage.getItem(BOARD_STATUSES_STORAGE_KEY) ?? 'null');
      const chosen = Array.isArray(stored) ? TASK_BOARD_STATUSES.filter((status) => stored.includes(status)) : [];

      return chosen.length > 0 ? chosen : DEFAULT_TASK_BOARD_STATUSES;
    } catch {
      // Whatever is stored there is not a list of statuses; the usual columns are shown.
      return DEFAULT_TASK_BOARD_STATUSES;
    }
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

    return this.filteredTasks()
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
