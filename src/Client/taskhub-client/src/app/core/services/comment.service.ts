import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { appSettings } from '../config/app-settings';
import { Comment } from '../models/comment.model';

@Injectable({ providedIn: 'root' })
export class CommentService {
  constructor(private readonly http: HttpClient) {}

  // Without a task the comments of the project itself are meant.
  getAll(projectId: string, taskId: string | null): Observable<Comment[]> {
    return this.http.get<Comment[]>(this.listUrl(projectId, taskId));
  }

  add(projectId: string, taskId: string | null, content: string): Observable<Comment> {
    return this.http.post<Comment>(this.listUrl(projectId, taskId), { content });
  }

  update(projectId: string, commentId: string, content: string): Observable<Comment> {
    return this.http.put<Comment>(`${this.projectUrl(projectId)}/comments/${commentId}`, { content });
  }

  delete(projectId: string, commentId: string): Observable<void> {
    return this.http.delete<void>(`${this.projectUrl(projectId)}/comments/${commentId}`);
  }

  private listUrl(projectId: string, taskId: string | null): string {
    return taskId ? `${this.projectUrl(projectId)}/tasks/${taskId}/comments` : `${this.projectUrl(projectId)}/comments`;
  }

  private projectUrl(projectId: string): string {
    return `${appSettings.apiUrl}/projects/${projectId}`;
  }
}
