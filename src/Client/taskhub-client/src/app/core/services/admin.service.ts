import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminUser, SetUserLockoutRequest, UpdateUserRolesRequest } from '../models/admin.model';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly baseUrl = `${environment.apiUrl}/admin/users`;

  constructor(private readonly http: HttpClient) {}

  getAllUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(this.baseUrl);
  }

  updateRoles(userId: string, request: UpdateUserRolesRequest): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.baseUrl}/${userId}/roles`, request);
  }

  setLockout(userId: string, request: SetUserLockoutRequest): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.baseUrl}/${userId}/lockout`, request);
  }

  deleteUser(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${userId}`);
  }
}
