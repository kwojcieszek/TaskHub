import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserSummary } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserDirectoryService {
  private readonly baseUrl = `${environment.apiUrl}/users`;

  constructor(private readonly http: HttpClient) {}

  search(query: string): Observable<UserSummary[]> {
    const params = new HttpParams().set('q', query);
    return this.http.get<UserSummary[]>(this.baseUrl, { params });
  }
}
