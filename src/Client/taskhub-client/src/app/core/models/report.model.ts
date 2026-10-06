import { TaskItemStatus } from './task.model';

export interface ReportRow {
  key: string;
  label: string;
  created: number;
  completed: number;
  due: number;
  overdue: number;
}

export interface ReportTaskRow {
  id: string;
  title: string;
  project: string;
  assignee: string;
  status: TaskItemStatus;
  dueDate: Date | null;
  startedAt: Date | null;
  completedAt: Date | null;
  // Time from start to completion; null when the task was completed without being started.
  durationMs: number | null;
  // Whole days past the due date: at completion for finished tasks, as of today for open ones. Null without a due date.
  delayDays: number | null;
}

export interface ReportSummary {
  key: 'created' | 'completed' | 'due' | 'overdue';
  icon: string;
  tone: string;
  value: number;
}

export interface ReportPeriod {
  // Inclusive start and exclusive end, in milliseconds; null means unbounded on that side.
  from: number | null;
  to: number | null;
}
