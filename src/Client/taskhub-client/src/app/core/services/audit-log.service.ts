import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { appSettings } from '../config/app-settings';
import { AuditLogQuery, PagedResult, UserActivityLog } from '../models/audit-log.model';

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly baseUrl = `${appSettings.apiUrl}/admin/logs`;

  constructor(private readonly http: HttpClient) {}

  getLogins(query: AuditLogQuery): Observable<PagedResult<UserActivityLog>> {
    return this.http.get<PagedResult<UserActivityLog>>(`${this.baseUrl}/logins`, { params: this.toParams(query) });
  }

  getMyLogins(query: AuditLogQuery): Observable<PagedResult<UserActivityLog>> {
    return this.http.get<PagedResult<UserActivityLog>>(`${appSettings.apiUrl}/auth/logins`, { params: this.toParams(query) });
  }

  getActivity(query: AuditLogQuery): Observable<PagedResult<UserActivityLog>> {
    return this.http.get<PagedResult<UserActivityLog>>(`${this.baseUrl}/activity`, { params: this.toParams(query) });
  }

  private toParams(query: AuditLogQuery): HttpParams {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);

    if (query.search) {
      params = params.set('search', query.search);
    }

    return params;
  }
}
