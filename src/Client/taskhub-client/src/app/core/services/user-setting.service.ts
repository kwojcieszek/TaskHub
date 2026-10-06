import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserSetting } from '../models/user-setting.model';

@Injectable({ providedIn: 'root' })
export class UserSettingService {
  private readonly baseUrl = `${environment.apiUrl}/settings`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<UserSetting[]> {
    return this.http.get<UserSetting[]>(this.baseUrl);
  }

  set(key: string, value: string): Observable<UserSetting> {
    return this.http.put<UserSetting>(`${this.baseUrl}/${encodeURIComponent(key)}`, { value });
  }

  delete(key: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(key)}`);
  }
}
