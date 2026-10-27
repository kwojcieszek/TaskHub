import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { appSettings } from '../config/app-settings';
import {
  AuthProviders,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  ResetPasswordRequest,
  TwoFactorSetupResponse,
  TwoFactorVerifyRequest
} from '../models/auth.model';
import { PasswordPolicy } from '../models/password-policy.model';
import { User } from '../models/user.model';
import { DEFAULT_PASSWORD_POLICY } from '../utils/password-policy';

const STORAGE_TOKEN_KEY = 'taskhub.accessToken';
const STORAGE_USER_KEY = 'taskhub.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = `${appSettings.apiUrl}/auth`;

  private readonly currentUserSignal = signal<User | null>(this.readStoredUser());
  private readonly accessTokenSignal = signal<string | null>(localStorage.getItem(STORAGE_TOKEN_KEY));

  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null && this.accessTokenSignal() !== null);
  readonly isAdmin = computed(() => this.currentUserSignal()?.roles.includes('Admin') ?? false);

  // Whether the server lets visitors create their own account; false until the server says otherwise.
  readonly registrationEnabled = signal(false);

  // Whether the server can e-mail password reset links; false until the server says otherwise.
  readonly passwordResetEnabled = signal(false);

  // The rules a new password must meet; the server is the one that enforces them.
  readonly passwordPolicy = signal<PasswordPolicy>(DEFAULT_PASSWORD_POLICY);

  constructor(private readonly http: HttpClient) {}

  loadProviders(): void {
    this.http.get<AuthProviders>(`${this.baseUrl}/providers`).subscribe({
      next: (providers) => {
        this.registrationEnabled.set(providers.registrationEnabled === true);
        this.passwordResetEnabled.set(providers.passwordResetEnabled === true);

        if (providers.passwordPolicy) {
          this.passwordPolicy.set(providers.passwordPolicy);
        }
      },
      error: () => {
        this.registrationEnabled.set(false);
        this.passwordResetEnabled.set(false);
      }
    });
  }

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

  linkGoogle(idToken: string, password: string, code?: string): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/google/link`, { idToken, password, code }).pipe(tap((user) => this.storeCurrentUser(user)));
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

  disableTwoFactor(password: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/2fa/disable`, { password }).pipe(
      tap(() => this.refreshCurrentUser().subscribe())
    );
  }

  // The server ends every other session and answers with a fresh token for this one.
  changePassword(currentPassword: string, newPassword: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/password`, { currentPassword, newPassword }).pipe(
      tap((response) => this.applyLoginResponse(response))
    );
  }

  // The server answers the same way whether or not the address belongs to an account.
  requestPasswordReset(email: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/password/forgot`, { email });
  }

  resetPassword(request: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/password/reset`, request);
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

  endSession(all = false): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/sessions/logout?all=${all}`, {}).pipe(tap(() => this.logout()));
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
    try { return raw ? (JSON.parse(raw) as User) : null; }
    catch { return null; }
  }
}
