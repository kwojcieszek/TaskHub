import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiToken, CreateApiTokenRequest, CreatedApiToken } from '../models/api-token.model';

@Injectable({ providedIn: 'root' })
export class ApiTokenService {
  private readonly baseUrl = `${environment.apiUrl}/tokens`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ApiToken[]> {
    return this.http.get<ApiToken[]>(this.baseUrl);
  }

  create(request: CreateApiTokenRequest): Observable<CreatedApiToken> {
    return this.http.post<CreatedApiToken>(this.baseUrl, request);
  }

  revoke(tokenId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${tokenId}`);
  }
}
