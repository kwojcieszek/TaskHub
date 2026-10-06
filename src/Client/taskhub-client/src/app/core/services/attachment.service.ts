import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Attachment } from '../models/attachment.model';

@Injectable({ providedIn: 'root' })
export class AttachmentService {
  constructor(private readonly http: HttpClient) {}

  private attachmentsUrl(projectId: string, taskId: string): string {
    return `${environment.apiUrl}/projects/${projectId}/tasks/${taskId}/attachments`;
  }

  getByTask(projectId: string, taskId: string): Observable<Attachment[]> {
    return this.http.get<Attachment[]>(this.attachmentsUrl(projectId, taskId));
  }

  upload(projectId: string, taskId: string, file: File): Observable<Attachment> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<Attachment>(this.attachmentsUrl(projectId, taskId), formData);
  }

  download(projectId: string, taskId: string, attachmentId: string): Observable<Blob> {
    return this.http.get(`${this.attachmentsUrl(projectId, taskId)}/${attachmentId}/content`, { responseType: 'blob' });
  }

  delete(projectId: string, taskId: string, attachmentId: string): Observable<void> {
    return this.http.delete<void>(`${this.attachmentsUrl(projectId, taskId)}/${attachmentId}`);
  }

  findBySha256(taskId: string, sha256: string): Observable<Attachment[]> {
    return this.http.get<Attachment[]>(`${environment.apiUrl}/attachments/${sha256}`, { params: { taskId } });
  }

  downloadBySha256(taskId: string, sha256: string): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/attachments/${sha256}/content`, { params: { taskId }, responseType: 'blob' });
  }
}
