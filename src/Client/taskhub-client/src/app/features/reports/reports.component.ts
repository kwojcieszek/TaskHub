import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { DatePicker } from 'primeng/datepicker';
import { TableModule } from 'primeng/table';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { LanguageService } from '../../core/services/language.service';
import { ProjectService } from '../../core/services/project.service';
import { TaskItemService } from '../../core/services/task-item.service';
import { ReportPeriod, ReportRow, ReportSummary, ReportTaskRow } from '../../core/models/report.model';
import { TaskItem } from '../../core/models/task.model';
import { TaskStatusFilter } from '../../core/models/task-status-filter.model';
import { StatusFilterComponent } from '../../shared/status-filter/status-filter.component';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';

const DEFAULT_MONTHS_BACK = 5;
const UNASSIGNED_KEY = '';
const MINUTE_MS = 60 * 1000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [LocalizedDatePipe, NgTemplateOutlet, FormsModule, Button, DatePicker, TableModule, TranslateModule, StatusFilterComponent],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss'
})
export class ReportsComponent implements OnInit {
  private readonly taskItemService = inject(TaskItemService);
  private readonly projectService = inject(ProjectService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  // Cancelled tasks are left out of every figure on this page.
  private readonly tasks = signal<TaskItem[]>([]);
  private readonly projectNames = signal<Record<string, string>>({});
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly taskPageSize = 20;

  // First day of the first and of the last month in the range; null leaves that side open.
  readonly fromMonth = signal<Date | null>(this.monthStart(new Date(), -DEFAULT_MONTHS_BACK));
  readonly toMonth = signal<Date | null>(this.monthStart(new Date(), 0));

  readonly statusFilter = signal<TaskStatusFilter>('All');

  // The status filter narrows the tasks behind every figure and table on the page.
  private readonly filteredTasks = computed(() => {
    const filter = this.statusFilter();

    return filter === 'All' ? this.tasks() : this.tasks().filter((task) => task.status === filter);
  });

  readonly period = computed<ReportPeriod>(() => {
    const from = this.fromMonth();
    const to = this.toMonth();

    return {
      from: from ? this.monthStart(from, 0).getTime() : null,
      to: to ? this.monthStart(to, 1).getTime() : null
    };
  });

  readonly summary = computed<ReportSummary[]>(() => {
    const total = this.count(this.filteredTasks(), this.period());

    return [
      { key: 'created', icon: 'pi pi-plus-circle', tone: 'blue', value: total.created },
      { key: 'completed', icon: 'pi pi-check', tone: 'green', value: total.completed },
      { key: 'due', icon: 'pi pi-calendar', tone: 'amber', value: total.due },
      { key: 'overdue', icon: 'pi pi-exclamation-circle', tone: 'red', value: total.overdue }
    ];
  });

  // Tasks finished in the period, newest first.
  readonly completedTasks = computed<ReportTaskRow[]>(() => {
    const period = this.period();

    return this.filteredTasks()
      .filter((task) => {
        const completed = this.completedTime(task);
        return completed !== null && this.inPeriod(completed, period);
      })
      .map((task) => this.toTaskRow(task))
      .sort((a, b) => (b.completedAt?.getTime() ?? 0) - (a.completedAt?.getTime() ?? 0));
  });

  // Unfinished tasks planned for the period: due in it, or created in it when they have no due date. Most delayed first.
  readonly openTasks = computed<ReportTaskRow[]>(() => {
    const period = this.period();

    return this.filteredTasks()
      .filter((task) => task.status !== 'Done')
      .filter((task) => this.inPeriod(this.dueTime(task) ?? this.createdTime(task), period))
      .map((task) => this.toTaskRow(task))
      .sort(
        (a, b) =>
          (b.delayDays ?? 0) - (a.delayDays ?? 0) ||
          (a.dueDate?.getTime() ?? Number.MAX_SAFE_INTEGER) - (b.dueDate?.getTime() ?? Number.MAX_SAFE_INTEGER)
      );
  });

  readonly byMonth = computed(() => {
    const formatter = new Intl.DateTimeFormat(this.language.current(), { month: 'long', year: 'numeric' });
    const period = this.period();
    const months = new Set<number>();

    for (const task of this.filteredTasks()) {
      for (const time of [this.createdTime(task), this.completedTime(task), this.dueTime(task)]) {
        if (time !== null && this.inPeriod(time, period)) {
          months.add(this.monthStart(new Date(time), 0).getTime());
        }
      }
    }

    return [...months]
      .sort((a, b) => b - a)
      .map((month) => {
        const start = new Date(month);
        const counts = this.count(this.filteredTasks(), { from: month, to: this.monthStart(start, 1).getTime() });

        return { key: String(month), label: formatter.format(start), ...counts };
      });
  });

  readonly byProject = computed(() =>
    this.group(
      (task) => task.projectId,
      (task) => this.projectNames()[task.projectId] ?? task.projectId
    )
  );

  readonly byAssignee = computed(() =>
    this.group(
      (task) => task.assignedUserId ?? UNASSIGNED_KEY,
      (task) => task.assignedUserDisplayName ?? this.translate.instant('tasks.unassigned')
    )
  );

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
        this.tasks.set(tasks.filter((task) => task.status !== 'Cancelled'));
        this.projectNames.set(Object.fromEntries(projects.map((project) => [project.id, project.name])));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      }
    });
  }

  showCurrentMonth(): void {
    const current = this.monthStart(new Date(), 0);
    this.fromMonth.set(current);
    this.toMonth.set(current);
  }

  showCurrentYear(): void {
    const year = new Date().getFullYear();
    this.fromMonth.set(new Date(year, 0, 1));
    this.toMonth.set(new Date(year, 11, 1));
  }

  showAll(): void {
    this.fromMonth.set(null);
    this.toMonth.set(null);
  }

  exportCsv(rows: ReportRow[], labelKey: string, fileName: string): void {
    const header = [
      this.translate.instant(labelKey),
      this.translate.instant('reports.metric.created'),
      this.translate.instant('reports.metric.completed'),
      this.translate.instant('reports.metric.due'),
      this.translate.instant('reports.metric.overdue')
    ];

    this.downloadCsv(
      header,
      rows.map((row) => [row.label, row.created, row.completed, row.due, row.overdue]),
      fileName
    );
  }

  exportTasksCsv(rows: ReportTaskRow[], fileName: string): void {
    const header = [
      this.translate.instant('reports.task'),
      this.translate.instant('reports.project'),
      this.translate.instant('reports.assignee'),
      this.translate.instant('tasks.status'),
      this.translate.instant('reports.dueDate'),
      this.translate.instant('reports.startedAt'),
      this.translate.instant('reports.completedAt'),
      this.translate.instant('reports.durationMinutes'),
      this.translate.instant('reports.delayDays')
    ];

    this.downloadCsv(
      header,
      rows.map((row) => [
        row.title,
        row.project,
        row.assignee,
        this.translate.instant('tasks.status.' + row.status),
        this.isoDate(row.dueDate),
        this.isoDateTime(row.startedAt),
        this.isoDateTime(row.completedAt),
        row.durationMs === null ? '' : Math.round(row.durationMs / MINUTE_MS),
        row.delayDays === null ? '' : Math.max(row.delayDays, 0)
      ]),
      fileName
    );
  }

  private toTaskRow(task: TaskItem): ReportTaskRow {
    const due = this.dueTime(task);
    const completed = this.completedTime(task);
    const started = task.startedAt ? this.utcTime(task.startedAt) : null;
    // A finished task is measured at its completion, an open one as of today.
    const reference = this.dayStart(completed ?? Date.now());

    return {
      id: task.id,
      title: task.title,
      project: this.projectNames()[task.projectId] ?? '',
      assignee: task.assignedUserDisplayName ?? this.translate.instant('tasks.unassigned'),
      status: task.status,
      dueDate: due === null ? null : new Date(due),
      startedAt: started === null ? null : new Date(started),
      completedAt: completed === null ? null : new Date(completed),
      durationMs: started === null || completed === null ? null : Math.max(completed - started, 0),
      delayDays: due === null ? null : Math.round((reference - this.dayStart(due)) / DAY_MS)
    };
  }

  formatDuration(durationMs: number): string {
    const days = Math.floor(durationMs / DAY_MS);
    const hours = Math.floor((durationMs % DAY_MS) / HOUR_MS);
    const minutes = Math.floor((durationMs % HOUR_MS) / MINUTE_MS);
    const seconds = Math.floor((durationMs % MINUTE_MS) / 1000);

    const parts = [
      days > 0 ? `${days} d` : '',
      days > 0 || hours > 0 ? `${hours} h` : '',
      `${minutes} min`,
      // Seconds only matter for tasks finished within the hour.
      days === 0 && hours === 0 ? `${seconds} s` : ''
    ];

    return parts.filter((part) => part !== '').join(' ');
  }

  private dayStart(time: number): number {
    const date = new Date(time);
    date.setHours(0, 0, 0, 0);
    return date.getTime();
  }

  private isoDate(date: Date | null): string {
    if (!date) {
      return '';
    }

    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');

    return `${date.getFullYear()}-${month}-${day}`;
  }

  private isoDateTime(date: Date | null): string {
    if (!date) {
      return '';
    }

    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');

    return `${this.isoDate(date)} ${hours}:${minutes}:${seconds}`;
  }

  private downloadCsv(header: string[], lines: (string | number)[][], fileName: string): void {
    const csv = [header, ...lines].map((line) => line.map((cell) => this.escapeCsv(cell)).join(';')).join('\r\n');

    // The BOM makes Excel read the file as UTF-8.
    const url = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  private group(keyOf: (task: TaskItem) => string, labelOf: (task: TaskItem) => string): ReportRow[] {
    const groups = new Map<string, { label: string; tasks: TaskItem[] }>();

    for (const task of this.filteredTasks()) {
      const key = keyOf(task);
      const group = groups.get(key);

      if (group) {
        group.tasks.push(task);
      } else {
        groups.set(key, { label: labelOf(task), tasks: [task] });
      }
    }

    const period = this.period();

    return [...groups.entries()]
      .map(([key, group]) => ({ key, label: group.label, ...this.count(group.tasks, period) }))
      .filter((row) => row.created + row.completed + row.due > 0)
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  // Each figure uses its own date: creation date, completion date, or due date falling in the period.
  private count(tasks: TaskItem[], period: ReportPeriod): Pick<ReportRow, 'created' | 'completed' | 'due' | 'overdue'> {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const counts = { created: 0, completed: 0, due: 0, overdue: 0 };

    for (const task of tasks) {
      const completed = this.completedTime(task);
      const due = this.dueTime(task);

      if (this.inPeriod(this.createdTime(task), period)) {
        counts.created++;
      }

      if (completed !== null && this.inPeriod(completed, period)) {
        counts.completed++;
      }

      if (due !== null && this.inPeriod(due, period)) {
        counts.due++;

        if (task.status !== 'Done' && due < today.getTime()) {
          counts.overdue++;
        }
      }
    }

    return counts;
  }

  private inPeriod(time: number, period: ReportPeriod): boolean {
    return (period.from === null || time >= period.from) && (period.to === null || time < period.to);
  }

  private createdTime(task: TaskItem): number {
    return this.utcTime(task.createdAt);
  }

  private completedTime(task: TaskItem): number | null {
    return task.status === 'Done' && task.completedAt ? this.utcTime(task.completedAt) : null;
  }

  private dueTime(task: TaskItem): number | null {
    return task.dueDate ? new Date(task.dueDate).getTime() : null;
  }

  // The API sends these timestamps in UTC but without a zone suffix.
  private utcTime(value: string): number {
    return new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : `${value}Z`).getTime();
  }

  private monthStart(date: Date, offsetMonths: number): Date {
    return new Date(date.getFullYear(), date.getMonth() + offsetMonths, 1);
  }

  private escapeCsv(value: string | number): string {
    const text = String(value);
    return /[";\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
  }
}
