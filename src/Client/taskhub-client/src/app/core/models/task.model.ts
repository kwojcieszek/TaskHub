export type TaskItemStatus = 'ToDo' | 'InProgress' | 'Done' | 'Cancelled';

export type TaskItemPriority = 'Low' | 'Medium' | 'High' | 'Critical';

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
  subTasks: TaskItem[];
}

export interface CreateTaskItemRequest {
  parentTaskId?: string | null;
  title: string;
  description?: string | null;
  dueDate?: string | null;
  priority: TaskItemPriority;
  assignedUserId?: string | null;
}

export interface UpdateTaskItemRequest {
  parentTaskId?: string | null;
  title: string;
  description?: string | null;
  dueDate?: string | null;
  status: TaskItemStatus;
  priority: TaskItemPriority;
  assignedUserId?: string | null;
}
