import { TaskItem, TaskItemStatus } from './task.model';

// A column of the board. Every status can have one; which ones are shown is chosen on the page.
export type TaskBoardStatus = TaskItemStatus;

// All columns in the order a task moves through them, and the ones shown until the user chooses otherwise.
export const TASK_BOARD_STATUSES: TaskBoardStatus[] = ['ToDo', 'InProgress', 'Done', 'Cancelled'];
export const DEFAULT_TASK_BOARD_STATUSES: TaskBoardStatus[] = ['ToDo', 'InProgress', 'Done'];

export interface TaskBoardColumn {
  status: TaskBoardStatus;
  icon: string;
  tasks: TaskItem[];
  // Tasks left out of a long column.
  hidden: number;
}

// A card dropped into another column.
export interface TaskBoardMove {
  task: TaskItem;
  status: TaskBoardStatus;
}
