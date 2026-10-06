import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SecuritySettings } from '../models/security-settings.model';

@Injectable({ providedIn: 'root' })
export class SecuritySettingsService {
  private readonly baseUrl = `${environment.apiUrl}/admin/security`;

  constructor(private readonly http: HttpClient) {}

  get(): Observable<SecuritySettings> {
    return this.http.get<SecuritySettings>(this.baseUrl);
  }

  update(settings: SecuritySettings): Observable<SecuritySettings> {
    return this.http.put<SecuritySettings>(this.baseUrl, settings);
  }
}
