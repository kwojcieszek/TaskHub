import { TaskItem } from './task.model';

export type DashboardScope = 'All' | 'Mine';

export interface DashboardSection {
  key: 'overdue' | 'today' | 'inProgress' | 'upcoming';
  icon: string;
  tone: string;
  tasks: TaskItem[];
}

export interface DashboardStat {
  key: DashboardSection['key'] | 'completedRecently';
  icon: string;
  tone: string;
  value: number;
}
