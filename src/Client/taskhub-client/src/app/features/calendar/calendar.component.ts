import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ProjectService } from '../../core/services/project.service';
import { TaskItemService } from '../../core/services/task-item.service';
import { TaskItem, TaskItemPriority, TaskItemStatus } from '../../core/models/task.model';
import { TaskStatusFilter } from '../../core/models/task-status-filter.model';
import { projectColor } from '../../core/utils/project-color';
import { StatusFilterComponent } from '../../shared/status-filter/status-filter.component';
import { TaskRowComponent } from '../../shared/task-row/task-row.component';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';

const STATUS_ORDER: Record<TaskItemStatus, number> = { InProgress: 0, ToDo: 1, Done: 2, Cancelled: 3 };
const PRIORITY_ORDER: Record<TaskItemPriority, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

interface CalendarDayTask {
  task: TaskItem;
  // True when the task is a subtask listed directly under its parent.
  nested: boolean;
}

interface CalendarDay {
  date: Date;
  inCurrentMonth: boolean;
  isToday: boolean;
  tasks: TaskItem[];
}

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [LocalizedDatePipe, Button, Dialog, TranslateModule, TaskRowComponent, StatusFilterComponent],
  templateUrl: './calendar.component.html',
  styleUrl: './calendar.component.scss'
})
export class CalendarComponent implements OnInit {
  private readonly taskItemService = inject(TaskItemService);
  private readonly projectService = inject(ProjectService);
  private readonly translate = inject(TranslateService);

  readonly viewDate = signal(new Date());
  readonly tasks = signal<TaskItem[]>([]);
  readonly selectedDate = signal<Date | null>(null);
  readonly statusFilter = signal<TaskStatusFilter>('All');
  private readonly projectNames = signal<Record<string, string>>({});

  // The status filter narrows both the month grid and the details of a day.
  private readonly filteredTasks = computed(() => {
    const filter = this.statusFilter();

    return filter === 'All' ? this.tasks() : this.tasks().filter((task) => task.status === filter);
  });

  // Details of the tasks due on the clicked day, shown in a modal window.
  readonly selectedTasks = computed<CalendarDayTask[]>(() => {
    const selected = this.selectedDate();

    if (!selected) {
      return [];
    }

    const key = selected.toDateString();
    const dayTasks = this.filteredTasks()
      .filter((task) => task.dueDate && new Date(task.dueDate).toDateString() === key)
      .sort((a, b) => this.compareTasks(a, b));
    const dayTaskIds = new Set(dayTasks.map((task) => task.id));

    // Each task is followed by its subtasks due the same day; a subtask whose parent is due another day stands alone.
    return dayTasks
      .filter((task) => task.parentTaskId === null || !dayTaskIds.has(task.parentTaskId))
      .flatMap((task) => [
        { task, nested: false },
        ...dayTasks.filter((subTask) => subTask.parentTaskId === task.id).map((subTask) => ({ task: subTask, nested: true }))
      ]);
  });

  // The date pipe has no Polish locale data, so the heading is formatted in the interface language directly.
  readonly selectedDateLabel = computed(() => {
    const selected = this.selectedDate();

    if (!selected) {
      return '';
    }

    return new Intl.DateTimeFormat(this.translate.currentLang || 'en', { dateStyle: 'full' }).format(selected);
  });

  readonly weeks = computed<CalendarDay[][]>(() => {
    const view = this.viewDate();
    const year = view.getFullYear();
    const month = view.getMonth();
    const firstOfMonth = new Date(year, month, 1);
    const startOffset = (firstOfMonth.getDay() + 6) % 7;
    const gridStart = new Date(year, month, 1 - startOffset);

    const tasksByDay = new Map<string, TaskItem[]>();
    for (const task of this.filteredTasks()) {
      if (!task.dueDate) {
        continue;
      }
      const key = new Date(task.dueDate).toDateString();
      const list = tasksByDay.get(key) ?? [];
      list.push(task);
      tasksByDay.set(key, list);
    }

    const today = new Date().toDateString();
    const days: CalendarDay[] = [];
    for (let i = 0; i < 42; i++) {
      const date = new Date(gridStart);
      date.setDate(gridStart.getDate() + i);
      days.push({
        date,
        inCurrentMonth: date.getMonth() === month,
        isToday: date.toDateString() === today,
        tasks: tasksByDay.get(date.toDateString()) ?? []
      });
    }

    const weeks: CalendarDay[][] = [];
    for (let i = 0; i < days.length; i += 7) {
      weeks.push(days.slice(i, i + 7));
    }
    return weeks;
  });

  weekdays(): string[] {
    const formatter = new Intl.DateTimeFormat(this.translate.currentLang || 'en', { weekday: 'short' });
    // 2024-01-01 was a Monday, so the list follows the Monday-first grid.
    return Array.from({ length: 7 }, (_, index) => formatter.format(new Date(2024, 0, 1 + index)));
  }

  isWeekend(date: Date): boolean {
    return date.getDay() === 0 || date.getDay() === 6;
  }

  isSelected(date: Date): boolean {
    return this.selectedDate()?.toDateString() === date.toDateString();
  }

  // Past its due date and still open, the same rule the task rows use.
  isOverdue(task: TaskItem): boolean {
    if (!task.dueDate || task.status === 'Done' || task.status === 'Cancelled') {
      return false;
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    return new Date(task.dueDate) < today;
  }

  selectDay(date: Date): void {
    this.selectedDate.set(date);
  }

  closeDetails(): void {
    this.selectedDate.set(null);
  }

  projectName(task: TaskItem): string {
    return this.projectNames()[task.projectId] ?? '';
  }

  projectColor(task: TaskItem): string {
    return projectColor(task.projectId);
  }

  ngOnInit(): void {
    this.load();
    this.projectService.getAll().subscribe((projects) => {
      this.projectNames.set(Object.fromEntries(projects.map((project) => [project.id, project.name])));
    });
  }

  previousMonth(): void {
    const view = this.viewDate();
    this.viewDate.set(new Date(view.getFullYear(), view.getMonth() - 1, 1));
    this.load();
  }

  nextMonth(): void {
    const view = this.viewDate();
    this.viewDate.set(new Date(view.getFullYear(), view.getMonth() + 1, 1));
    this.load();
  }

  today(): void {
    this.viewDate.set(new Date());
    this.load();
  }

  // Open work first (in progress, then to do), closed last; within a status the most urgent and then alphabetically.
  private compareTasks(a: TaskItem, b: TaskItem): number {
    return (
      STATUS_ORDER[a.status] - STATUS_ORDER[b.status] ||
      PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority] ||
      a.title.localeCompare(b.title)
    );
  }

  private load(): void {
    const view = this.viewDate();
    const from = new Date(view.getFullYear(), view.getMonth() - 1, 1);
    const to = new Date(view.getFullYear(), view.getMonth() + 2, 0);
    this.taskItemService
      .getAcrossProjects({ from: from.toISOString(), to: to.toISOString() })
      .subscribe((tasks) => this.tasks.set(tasks));
  }
}
