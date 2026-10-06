import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateProjectGroupRequest, ProjectGroup, UpdateProjectGroupRequest } from '../models/project-group.model';

@Injectable({ providedIn: 'root' })
export class ProjectGroupService {
  private readonly baseUrl = `${environment.apiUrl}/project-groups`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ProjectGroup[]> {
    return this.http.get<ProjectGroup[]>(this.baseUrl);
  }

  create(request: CreateProjectGroupRequest): Observable<ProjectGroup> {
    return this.http.post<ProjectGroup>(this.baseUrl, request);
  }

  update(projectGroupId: string, request: UpdateProjectGroupRequest): Observable<ProjectGroup> {
    return this.http.put<ProjectGroup>(`${this.baseUrl}/${projectGroupId}`, request);
  }

  delete(projectGroupId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${projectGroupId}`);
  }
}
