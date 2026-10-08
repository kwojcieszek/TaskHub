import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { User } from '../models/user.model';
import { AuthService } from './auth.service';

const TOKEN_KEY = 'taskhub.accessToken';
const USER_KEY = 'taskhub.user';

function user(overrides: Partial<User> = {}): User {
  return {
    id: 'u1',
    userName: 'anna',
    email: 'anna@example.test',
    displayName: 'Anna',
    twoFactorEnabled: false,
    mustChangePassword: false,
    googleLinked: false,
    googleEmail: null,
    roles: ['User'],
    ...overrides
  };
}

describe('AuthService', () => {
  const authUrl = `${environment.apiUrl}/auth`;

  let http: HttpTestingController;

  function create(): AuthService {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);

    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('starts signed out when nothing is stored', () => {
    const service = create();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.isAdmin()).toBeFalse();
    expect(service.accessToken).toBeNull();
  });

  it('restores the session that was stored', () => {
    localStorage.setItem(TOKEN_KEY, 'stored-token');
    localStorage.setItem(USER_KEY, JSON.stringify(user({ roles: ['Admin'] })));

    const service = create();

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.isAdmin()).toBeTrue();
    expect(service.accessToken).toBe('stored-token');
  });

  it('is not signed in with a stored user but no token', () => {
    localStorage.setItem(USER_KEY, JSON.stringify(user()));

    expect(create().isAuthenticated()).toBeFalse();
  });

  it('signs in and remembers the token and the user', () => {
    const service = create();

    service.login({ userName: 'anna', password: 'secret' }).subscribe();
    const request = http.expectOne(`${authUrl}/login`);
    expect(request.request.body).toEqual({ userName: 'anna', password: 'secret' });
    request.flush({ requiresTwoFactor: false, accessToken: 'token', user: user() });

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.isAdmin()).toBeFalse();
    expect(service.currentUser()?.userName).toBe('anna');
    expect(localStorage.getItem(TOKEN_KEY)).toBe('token');
    expect(JSON.parse(localStorage.getItem(USER_KEY) ?? '{}').id).toBe('u1');
  });

  it('stays signed out while the second factor is still owed', () => {
    const service = create();

    service.login({ userName: 'anna', password: 'secret' }).subscribe();
    http.expectOne(`${authUrl}/login`).flush({ requiresTwoFactor: true, twoFactorToken: 'pending' });

    expect(service.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
  });

  it('signs in after the second factor is verified', () => {
    const service = create();

    service.verifyTwoFactor({ twoFactorToken: 'pending', code: '123456' }).subscribe();
    http.expectOne(`${authUrl}/2fa/verify`).flush({ requiresTwoFactor: false, accessToken: 'token', user: user() });

    expect(service.isAuthenticated()).toBeTrue();
  });

  it('continues with the new token after the password is changed', () => {
    localStorage.setItem(TOKEN_KEY, 'old-token');
    localStorage.setItem(USER_KEY, JSON.stringify(user({ mustChangePassword: true })));
    const service = create();

    service.changePassword('old', 'new').subscribe();
    const request = http.expectOne(`${authUrl}/password`);
    expect(request.request.body).toEqual({ currentPassword: 'old', newPassword: 'new' });
    request.flush({ requiresTwoFactor: false, accessToken: 'new-token', user: user({ mustChangePassword: false }) });

    expect(service.accessToken).toBe('new-token');
    expect(service.currentUser()?.mustChangePassword).toBeFalse();
  });

  it('forgets everything on logout', () => {
    localStorage.setItem(TOKEN_KEY, 'stored-token');
    localStorage.setItem(USER_KEY, JSON.stringify(user({ roles: ['Admin'] })));
    const service = create();

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.isAdmin()).toBeFalse();
    expect(service.accessToken).toBeNull();
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
    expect(localStorage.getItem(USER_KEY)).toBeNull();
  });

  it('takes registration and the password policy from the server, and turns registration off when it cannot be asked', () => {
    const service = create();

    service.loadProviders();
    http.expectOne(`${authUrl}/providers`).flush({
      googleClientId: null,
      registrationEnabled: true,
      passwordPolicy: { minLength: 12, requireUppercase: false, requireLowercase: true, requireDigit: true, requireNonAlphanumeric: true }
    });

    expect(service.registrationEnabled()).toBeTrue();
    expect(service.passwordPolicy().minLength).toBe(12);

    service.loadProviders();
    http.expectOne(`${authUrl}/providers`).error(new ProgressEvent('error'));

    expect(service.registrationEnabled()).toBeFalse();
  });
});
