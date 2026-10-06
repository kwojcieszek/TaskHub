import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  TwoFactorSetupResponse,
  TwoFactorVerifyRequest
} from '../models/auth.model';
import { User } from '../models/user.model';

const STORAGE_TOKEN_KEY = 'taskhub.accessToken';
const STORAGE_USER_KEY = 'taskhub.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private readonly currentUserSignal = signal<User | null>(this.readStoredUser());
  private readonly accessTokenSignal = signal<string | null>(localStorage.getItem(STORAGE_TOKEN_KEY));

  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null && this.accessTokenSignal() !== null);
  readonly isAdmin = computed(() => this.currentUserSignal()?.roles.includes('Admin') ?? false);

  constructor(private readonly http: HttpClient) {}

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  register(request: RegisterRequest): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/register`, request);
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request).pipe(
      tap((response) => this.applyLoginResponse(response))
    );
  }

  loginWithGoogle(idToken: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/google/login`, { idToken }).pipe(
      tap((response) => this.applyLoginResponse(response))
    );
  }

  linkGoogle(idToken: string): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/google/link`, { idToken }).pipe(tap((user) => this.storeCurrentUser(user)));
  }

  unlinkGoogle(): Observable<User> {
    return this.http.delete<User>(`${this.baseUrl}/google/link`).pipe(tap((user) => this.storeCurrentUser(user)));
  }

  verifyTwoFactor(request: TwoFactorVerifyRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/2fa/verify`, request).pipe(
      tap((response) => this.applyLoginResponse(response))
    );
  }

  beginTwoFactorSetup(): Observable<TwoFactorSetupResponse> {
    return this.http.post<TwoFactorSetupResponse>(`${this.baseUrl}/2fa/setup`, {});
  }

  enableTwoFactor(code: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/2fa/enable`, { code }).pipe(
      tap(() => this.refreshCurrentUser().subscribe())
    );
  }

  disableTwoFactor(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/2fa/disable`, {}).pipe(
      tap(() => this.refreshCurrentUser().subscribe())
    );
  }

  refreshCurrentUser(): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/me`).pipe(tap((user) => this.storeCurrentUser(user)));
  }

  logout(): void {
    this.currentUserSignal.set(null);
    this.accessTokenSignal.set(null);
    localStorage.removeItem(STORAGE_TOKEN_KEY);
    localStorage.removeItem(STORAGE_USER_KEY);
  }

  private storeCurrentUser(user: User): void {
    this.currentUserSignal.set(user);
    localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(user));
  }

  private applyLoginResponse(response: LoginResponse): void {
    if (response.requiresTwoFactor || !response.accessToken || !response.user) {
      return;
    }

    this.accessTokenSignal.set(response.accessToken);
    this.currentUserSignal.set(response.user);
    localStorage.setItem(STORAGE_TOKEN_KEY, response.accessToken);
    localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(response.user));
  }

  private readStoredUser(): User | null {
    const raw = localStorage.getItem(STORAGE_USER_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  }
}
