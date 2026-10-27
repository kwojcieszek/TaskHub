export type TaskItemStatus = 'ToDo' | 'InProgress' | 'Done' | 'Cancelled';

export type TaskItemPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export type RecurrenceUnit = 'Day' | 'Week' | 'Month' | 'Year';

// The date the interval is counted from: the day the task was completed, or its due date.
export type RecurrenceBasis = 'CompletionDate' | 'DueDate';

export interface TaskRecurrence {
  interval: number;
  unit: RecurrenceUnit;
  basis: RecurrenceBasis;
}

export interface TaskItem {
  id: string;
  projectId: string;
  parentTaskId: string | null;
  title: string;
  description: string | null;
  dueDate: string | null;
  status: TaskItemStatus;
  priority: TaskItemPriority;
  assignedUserId: string | null;
  assignedUserDisplayName: string | null;
  createdAt: string;
  updatedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  attachmentCount: number;
  recurrence: TaskRecurrence | null;
  subTasks: TaskItem[];
}

export interface CreateTaskItemRequest {
  parentTaskId?: string | null;
  title: string;
  description?: string | null;
  dueDate?: string | null;
  priority: TaskItemPriority;
  assignedUserId?: string | null;
  recurrence?: TaskRecurrence | null;
}

export interface UpdateTaskItemRequest {
  parentTaskId?: string | null;
  title: string;
  description?: string | null;
  dueDate?: string | null;
  status: TaskItemStatus;
  priority: TaskItemPriority;
  assignedUserId?: string | null;
  // Always sent: leaving it out would stop the task from repeating.
  recurrence: TaskRecurrence | null;
}
