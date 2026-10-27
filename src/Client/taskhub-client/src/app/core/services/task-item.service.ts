import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateTaskItemRequest, TaskItem, UpdateTaskItemRequest, TaskItemStatus } from '../models/task.model';

@Injectable({ providedIn: 'root' })
export class TaskItemService {
  constructor(private readonly http: HttpClient) {}

  private tasksUrl(projectId: string): string {
    return `${environment.apiUrl}/projects/${projectId}/tasks`;
  }

  getByProject(projectId: string): Observable<TaskItem[]> {
    return this.http.get<TaskItem[]>(this.tasksUrl(projectId));
  }

  getById(projectId: string, taskId: string): Observable<TaskItem> {
    return this.http.get<TaskItem>(`${this.tasksUrl(projectId)}/${taskId}`);
  }

  create(projectId: string, request: CreateTaskItemRequest): Observable<TaskItem> {
    return this.http.post<TaskItem>(this.tasksUrl(projectId), request);
  }

  update(projectId: string, taskId: string, request: UpdateTaskItemRequest): Observable<TaskItem> {
    return this.http.put<TaskItem>(`${this.tasksUrl(projectId)}/${taskId}`, request);
  }

  delete(projectId: string, taskId: string): Observable<void> {
    return this.http.delete<void>(`${this.tasksUrl(projectId)}/${taskId}`);
  }

  // Tasks from every project the user can see; with a date range, only the ones due within it.
  getAcrossProjects(range?: { from: string; to: string }): Observable<TaskItem[]> {
    const params = range ? new HttpParams().set('from', range.from).set('to', range.to) : undefined;

    return this.http.get<TaskItem[]>(`${environment.apiUrl}/tasks`, { params });
  }

  start(projectId: string, taskId: string): Observable<TaskItem> {
    return this.http.post<TaskItem>(`${this.tasksUrl(projectId)}/${taskId}/start`, {});
  }

  complete(projectId: string, taskId: string): Observable<TaskItem> {
    return this.http.post<TaskItem>(`${this.tasksUrl(projectId)}/${taskId}/complete`, {});
  }

  // Moves a task to another status the way the task buttons do: starting and completing have their own
  // operations, which also record when they happened; any other change is an ordinary update.
  setStatus(task: TaskItem, status: TaskItemStatus): Observable<TaskItem> {
    if (status === 'InProgress' && task.status === 'ToDo') {
      return this.start(task.projectId, task.id);
    }

    if (status === 'Done') {
      return this.complete(task.projectId, task.id);
    }

    return this.update(task.projectId, task.id, {
      parentTaskId: task.parentTaskId,
      title: task.title,
      description: task.description,
      dueDate: task.dueDate,
      status,
      priority: task.priority,
      assignedUserId: task.assignedUserId,
      recurrence: task.recurrence
    });
  }

  // Undoes a completion: back to "in progress" when the task had been started, otherwise to "to do".
  reopen(task: TaskItem): Observable<TaskItem> {
    return this.update(task.projectId, task.id, {
      parentTaskId: task.parentTaskId,
      title: task.title,
      description: task.description,
      dueDate: task.dueDate,
      status: task.startedAt ? 'InProgress' : 'ToDo',
      priority: task.priority,
      assignedUserId: task.assignedUserId,
      recurrence: task.recurrence
    });
  }
}
