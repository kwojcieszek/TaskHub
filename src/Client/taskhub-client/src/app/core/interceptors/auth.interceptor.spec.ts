import { appSettings } from '../config/app-settings';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: { accessToken: string | null; isAuthenticated: jasmine.Spy; logout: jasmine.Spy };
  let router: { navigate: jasmine.Spy };

  beforeEach(() => {
    auth = { accessToken: 'token', isAuthenticated: jasmine.createSpy().and.returnValue(true), logout: jasmine.createSpy() };
    router = { navigate: jasmine.createSpy() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router }
      ]
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('sends the access token with a request', () => {
    http.get(`${appSettings.apiUrl}/projects`).subscribe();

    const request = backend.expectOne(`${appSettings.apiUrl}/projects`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer token');
    request.flush([]);
  });

  it('does not send credentials or log out on a third-party 401', () => {
    http.get('https://external.example/api/v1/projects').subscribe({error: () => undefined});
    const request = backend.expectOne('https://external.example/api/v1/projects');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({}, {status: 401, statusText: 'Unauthorized'});
    expect(auth.logout).not.toHaveBeenCalled();
  });

  it('sends no Authorization header when nobody is signed in', () => {
    auth.accessToken = null;

    http.get(`${appSettings.apiUrl}/auth/providers`).subscribe();

    const request = backend.expectOne(`${appSettings.apiUrl}/auth/providers`);
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('signs the user out and goes to the login page when the session is refused', () => {
    let failed = false;

    http.get(`${appSettings.apiUrl}/projects`).subscribe({ error: () => (failed = true) });
    backend.expectOne(`${appSettings.apiUrl}/projects`).flush({ errors: ['expired'] }, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBeTrue();
    expect(auth.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('leaves a failed sign-in attempt to the login page', () => {
    auth.accessToken = null;
    auth.isAuthenticated.and.returnValue(false);

    http.post(`${appSettings.apiUrl}/auth/login`, {}).subscribe({ error: () => undefined });
    backend.expectOne(`${appSettings.apiUrl}/auth/login`).flush({ errors: ['invalid'] }, { status: 401, statusText: 'Unauthorized' });

    expect(auth.logout).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('keeps the session on other errors and passes them on', () => {
    let status = 0;

    http.get(`${appSettings.apiUrl}/projects/1`).subscribe({ error: (error) => (status = error.status) });
    backend.expectOne(`${appSettings.apiUrl}/projects/1`).flush({ errors: ['forbidden'] }, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    expect(auth.logout).not.toHaveBeenCalled();
  });
});
