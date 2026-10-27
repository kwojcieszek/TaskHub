import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { appSettings } from '../config/app-settings';
import { SecuritySettings } from '../models/security-settings.model';

@Injectable({ providedIn: 'root' })
export class SecuritySettingsService {
  private readonly baseUrl = `${appSettings.apiUrl}/admin/security`;

  constructor(private readonly http: HttpClient) {}

  get(): Observable<SecuritySettings> {
    return this.http.get<SecuritySettings>(this.baseUrl);
  }

  update(settings: SecuritySettings): Observable<SecuritySettings> {
    return this.http.put<SecuritySettings>(this.baseUrl, settings);
  }
}
