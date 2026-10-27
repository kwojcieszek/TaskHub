import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddUserGroupMemberRequest,
  CreateUserGroupRequest,
  UpdateUserGroupRequest,
  UserGroup,
  UserGroupMember
} from '../models/user-group.model';

@Injectable({ providedIn: 'root' })
export class UserGroupService {
  private readonly baseUrl = `${environment.apiUrl}/user-groups`;

  constructor(private readonly http: HttpClient) {}

  transferOwner(groupId: string, userId: string): Observable<UserGroup> { return this.http.put<UserGroup>(`${this.baseUrl}/${groupId}/owner`, { userId }); }

  leave(groupId: string): Observable<void> { return this.http.post<void>(`${this.baseUrl}/${groupId}/leave`, {}); }

  getAll(): Observable<UserGroup[]> {
    return this.http.get<UserGroup[]>(this.baseUrl);
  }

  create(request: CreateUserGroupRequest): Observable<UserGroup> {
    return this.http.post<UserGroup>(this.baseUrl, request);
  }

  update(groupId: string, request: UpdateUserGroupRequest): Observable<UserGroup> {
    return this.http.put<UserGroup>(`${this.baseUrl}/${groupId}`, request);
  }

  delete(groupId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${groupId}`);
  }

  getMembers(groupId: string): Observable<UserGroupMember[]> {
    return this.http.get<UserGroupMember[]>(`${this.baseUrl}/${groupId}/members`);
  }

  addMember(groupId: string, request: AddUserGroupMemberRequest): Observable<UserGroupMember> {
    return this.http.post<UserGroupMember>(`${this.baseUrl}/${groupId}/members`, request);
  }

  removeMember(groupId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${groupId}/members/${userId}`);
  }
}
