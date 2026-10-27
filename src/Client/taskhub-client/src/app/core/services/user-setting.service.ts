import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { appSettings } from '../config/app-settings';
import { UserSetting } from '../models/user-setting.model';

@Injectable({ providedIn: 'root' })
export class UserSettingService {
  private readonly baseUrl = `${appSettings.apiUrl}/settings`;

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
