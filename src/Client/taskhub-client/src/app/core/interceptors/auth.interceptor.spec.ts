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
    http.get('/api/v1/projects').subscribe();

    const request = backend.expectOne('/api/v1/projects');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token');
    request.flush([]);
  });

  it('sends no Authorization header when nobody is signed in', () => {
    auth.accessToken = null;

    http.get('/api/v1/auth/providers').subscribe();

    const request = backend.expectOne('/api/v1/auth/providers');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('signs the user out and goes to the login page when the session is refused', () => {
    let failed = false;

    http.get('/api/v1/projects').subscribe({ error: () => (failed = true) });
    backend.expectOne('/api/v1/projects').flush({ errors: ['expired'] }, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBeTrue();
    expect(auth.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('leaves a failed sign-in attempt to the login page', () => {
    auth.accessToken = null;
    auth.isAuthenticated.and.returnValue(false);

    http.post('/api/v1/auth/login', {}).subscribe({ error: () => undefined });
    backend.expectOne('/api/v1/auth/login').flush({ errors: ['invalid'] }, { status: 401, statusText: 'Unauthorized' });

    expect(auth.logout).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('keeps the session on other errors and passes them on', () => {
    let status = 0;

    http.get('/api/v1/projects/1').subscribe({ error: (error) => (status = error.status) });
    backend.expectOne('/api/v1/projects/1').flush({ errors: ['forbidden'] }, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    expect(auth.logout).not.toHaveBeenCalled();
  });
});
