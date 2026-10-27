import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { appSettings } from '../config/app-settings';
import {
  AddProjectMemberRequest,
  AddProjectTeamRequest,
  CreateProjectRequest,
  Project,
  ProjectMember,
  ProjectTeam,
  UpdateProjectRequest
} from '../models/project.model';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly baseUrl = `${appSettings.apiUrl}/projects`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<Project[]> {
    return this.http.get<Project[]>(this.baseUrl);
  }

  getById(projectId: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/${projectId}`);
  }

  create(request: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(this.baseUrl, request);
  }

  update(projectId: string, request: UpdateProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/${projectId}`, request);
  }

  setGroup(projectId: string, projectGroupId: string | null): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/${projectId}/group`, { projectGroupId });
  }

  delete(projectId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${projectId}`);
  }

  getMembers(projectId: string): Observable<ProjectMember[]> {
    return this.http.get<ProjectMember[]>(`${this.baseUrl}/${projectId}/members`);
  }

  addMember(projectId: string, request: AddProjectMemberRequest): Observable<ProjectMember> {
    return this.http.post<ProjectMember>(`${this.baseUrl}/${projectId}/members`, request);
  }

  removeMember(projectId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${projectId}/members/${userId}`);
  }

  getTeams(projectId: string): Observable<ProjectTeam[]> {
    return this.http.get<ProjectTeam[]>(`${this.baseUrl}/${projectId}/teams`);
  }

  // The teams that can still be added; only the owner of the project may ask.
  getAvailableTeams(projectId: string): Observable<ProjectTeam[]> {
    return this.http.get<ProjectTeam[]>(`${this.baseUrl}/${projectId}/teams/available`);
  }

  addTeam(projectId: string, request: AddProjectTeamRequest): Observable<ProjectTeam> {
    return this.http.post<ProjectTeam>(`${this.baseUrl}/${projectId}/teams`, request);
  }

  removeTeam(projectId: string, userGroupId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${projectId}/teams/${userGroupId}`);
  }
}
