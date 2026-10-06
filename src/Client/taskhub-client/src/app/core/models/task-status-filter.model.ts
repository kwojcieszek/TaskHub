import { TaskItemStatus } from './task.model';

export type TaskStatusFilter = 'All' | Extract<TaskItemStatus, 'ToDo' | 'InProgress' | 'Done'>;

export const TASK_STATUS_FILTERS: TaskStatusFilter[] = ['All', 'ToDo', 'InProgress', 'Done'];
